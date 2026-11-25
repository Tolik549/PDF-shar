using Docnet.Core;
using Docnet.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Tesseract;

namespace PdfShark.Services;

/// <summary>
/// OCR-распознавание сканированных PDF: рендерит страницы через PDFium (Docnet.Core)
/// и распознаёт текст через Tesseract (языки eng+rus).
/// </summary>
public sealed class OcrService
{
    private readonly string _tessDataPath;

    /// <summary>Есть ли языковые данные Tesseract (tessdata/eng.traineddata и rus.traineddata).</summary>
    public bool HasLanguageData { get; }

    public OcrService()
    {
        _tessDataPath = ResolveTessDataPath();
        HasLanguageData =
            File.Exists(Path.Combine(_tessDataPath, "eng.traineddata")) &&
            File.Exists(Path.Combine(_tessDataPath, "rus.traineddata"));
    }

    /// <summary>
    /// Определяет каталог с языковыми данными. Сначала ищет папку tessdata рядом
    /// с приложением, иначе извлекает встроенные ресурсы eng/rus в %LocalAppData%.
    /// </summary>
    private static string ResolveTessDataPath()
    {
        string localDir = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (File.Exists(Path.Combine(localDir, "eng.traineddata")) &&
            File.Exists(Path.Combine(localDir, "rus.traineddata")))
        {
            return localDir;
        }

        string targetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfShark", "tessdata");
        Directory.CreateDirectory(targetDir);
        ExtractIfMissing("PdfShark.tessdata.eng.traineddata", Path.Combine(targetDir, "eng.traineddata"));
        ExtractIfMissing("PdfShark.tessdata.rus.traineddata", Path.Combine(targetDir, "rus.traineddata"));
        return targetDir;
    }

    private static void ExtractIfMissing(string resourceName, string destFile)
    {
        if (File.Exists(destFile))
        {
            return;
        }

        using var stream = typeof(OcrService).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }

        using (var file = File.Create(destFile))
        {
            stream.CopyTo(file);
        }
    }

    /// <summary>Путь к каталогу с языковыми данными OCR.</summary>
    public string TessDataPath => _tessDataPath;

    /// <summary>Распознаёт текст со всех страниц PDF. Возвращает пустую строку, если данных OCR нет.</summary>
    public string OcrPdf(string pdfPath)
    {
        if (!HasLanguageData)
        {
            AppLog.Info("OCR пропущен: нет tessdata в " + _tessDataPath);
            return string.Empty;
        }

        var result = new StringBuilder();
        TesseractEngine? engine = null;
        try
        {
            engine = new TesseractEngine(_tessDataPath, "eng+rus", EngineMode.Default);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Ошибка инициализации Tesseract (tessdata={_tessDataPath})", ex);
            throw;
        }

        using (engine)
        {
            Docnet.Core.Readers.IDocReader? docReader = null;
            try
            {
                docReader = DocLib.Instance.GetDocReader(
                    pdfPath,
                    new Docnet.Core.Models.PageDimensions(1080, 1920));
            }
            catch (Exception ex)
            {
                AppLog.Error($"Ошибка открытия PDF для рендера '{pdfPath}'", ex);
                throw;
            }

            using (docReader)
            {
                var pageCount = docReader.GetPageCount();
                for (int i = 0; i < pageCount; i++)
                {
                    try
                    {
                        string? pageText = ProcessPage(engine, docReader, i, pdfPath);
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            result.Append(pageText);
                            result.AppendLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Пропускаем сбойную страницу, продолжаем остальные.
                        AppLog.Error($"Ошибка OCR страницы {i} документа '{pdfPath}'", ex);
                    }
                }
            }
        }

        return result.ToString();
    }

    private static string? ProcessPage(
        TesseractEngine engine,
        Docnet.Core.Readers.IDocReader docReader,
        int index,
        string pdfPath)
    {
        byte[]? raw;
        int width;
        int height;
        using (var pageReader = docReader.GetPageReader(index))
        {
            raw = pageReader.GetImage();
            width = pageReader.GetPageWidth();
            height = pageReader.GetPageHeight();
        }

        if (raw is null || raw.Length == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        using var bmp = CreateBitmap(raw, width, height);
        using var pix = PixConverter.ToPix(bmp);
        using var page = engine.Process(pix);
        var text = page.GetText();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static Bitmap CreateBitmap(byte[] bgra, int width, int height)
    {
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, width, height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            int bytes = Math.Min(bgra.Length, data.Stride * height);
            Marshal.Copy(bgra, 0, data.Scan0, bytes);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }
}