using PdfShark.Models;

namespace PdfShark.Services;

/// <summary>
/// Координатор: сканирует папку, распознаёт документы (текст из PDF или OCR),
/// определяет тему/краткое содержание и поддерживает кэш результатов.
/// </summary>
public sealed class FolderDocumentProcessor : IDisposable
{
    private readonly IPdfTextExtractor _textExtractor;
    private readonly OcrService _ocr;
    private readonly IThemeAnalyzer _analyzer;
    private readonly DocumentCache _cache;

    public FolderDocumentProcessor()
        : this(new PdfPigTextExtractor(), new OcrService(), new SimpleTextAnalyzer(), new DocumentCache())
    {
    }

    public FolderDocumentProcessor(
        IPdfTextExtractor textExtractor,
        OcrService ocr,
        IThemeAnalyzer analyzer,
        DocumentCache cache)
    {
        _textExtractor = textExtractor;
        _ocr = ocr;
        _analyzer = analyzer;
        _cache = cache;
    }

    /// <summary>Есть ли языковые данные для OCR (tessdata) рядом с приложением.</summary>
    public bool HasOcrData => _ocr.HasLanguageData;

    /// <summary>Путь к каталогу tessdata (для информации пользователю).</summary>
    public string TessDataPath => _ocr.TessDataPath;

    /// <summary>
    /// Сканирует папку на предмет *.pdf, распознаёт новые/изменённые документы
    /// и возвращает отсортированный список. Выполняется синхронно (вызывать в фоне).
    /// </summary>
    public IReadOnlyList<DocumentInfo> RefreshFolder(
        string folder,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var pdfs = Directory
            .EnumerateFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

        var result = new List<DocumentInfo>();
        foreach (string path in pdfs)
        {
            ct.ThrowIfCancellationRequested();
            var fi = new FileInfo(path);

            // Уже распознанный и неизменённый документ — берём из кэша.
            if (_cache.TryGet(path, fi.LastWriteTimeUtc, out var cached))
            {
                result.Add(cached);
                continue;
            }

            progress?.Report($"Обработка: {fi.Name} ...");
            var doc = ProcessDocument(path, fi);
            _cache.Set(doc);
            result.Add(doc);
        }

        _cache.Save();
        return result;
    }

    private DocumentInfo ProcessDocument(string path, FileInfo fi)
    {
        string name = fi.Name;
        var doc = new DocumentInfo
        {
            FileName = name,
            FullPath = path,
            Modified = fi.LastWriteTime,
            Status = DocumentStatus.Processing
        };

        try
        {
            if (!string.Equals(fi.Extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                doc.Status = DocumentStatus.NotPdf;
                return doc;
            }

            // 1. Сначала пробуем готовый текстовый слой PDF.
            string text = string.Empty;
            bool textLayerFailed = false;
            try
            {
                text = _textExtractor.ExtractText(path);
            }
            catch (Exception textEx)
            {
                // Скан или PDF, который PdfPig не смог разобрать, — пробуем OCR.
                textLayerFailed = true;
                AppLog.Error($"Текстовый слой недоступен для '{doc.FileName}'; пробуем OCR", textEx);
            }

            // 2. Если текста нет — это скан, подключаем OCR.
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!_ocr.HasLanguageData)
                {
                    doc.Status = DocumentStatus.OcrDataMissing;
                    doc.Error = textLayerFailed
                        ? "Не удалось открыть PDF и нет данных OCR (tessdata)"
                        : null;
                    return doc;
                }

                text = _ocr.OcrPdf(path);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                doc.Status = DocumentStatus.NoText;
                return doc;
            }

            // 3. Определяем тему и краткое содержание.
            doc.Text = text;
            var analysis = _analyzer.Analyze(text);
            doc.Theme = analysis.Theme;
            doc.Summary = analysis.Summary;
            doc.Status = DocumentStatus.Ready;
        }
        catch (Exception ex)
        {
            doc.Status = DocumentStatus.Error;
            doc.Error = Flatten(ex);
            AppLog.Error($"Ошибка при обработке '{doc.FileName}'", ex);
        }

        return doc;
    }

    /// <summary>Читаемая цепочка сообщений исключения (с учётом InnerException).</summary>
    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            parts.Add(e.Message);
        }
        return string.Join(" | ", parts);
    }

    public void Dispose()
    {
        _cache.Save();
    }
}