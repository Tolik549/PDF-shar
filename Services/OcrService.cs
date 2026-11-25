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
            return string.Empty;
        }

        var result = new StringBuilder();
        using var engine = new TesseractEngine(_tessDataPath, "eng+rus", EngineMode.Default);

        // Рендер страниц PDF в растровое изображение.
        using var docReader = DocLib.Instance.GetDocReader(
            pdfPath,
            new PageDimensions(1080, 1920));

        var pageCount = docReader.GetPageCount();
        for (int i = 0; i < pageCount; i++)
        {
            byte[]? raw;
            int width;
            int height;

            try
            {
                using (var pageReader = docReader.GetPageReader(i))
                {
                    raw = pageReader.GetImage();
                    width = pageReader.GetPageWidth();
                    height = pageReader.GetPageHeight();
                }
            }
            catch
            {
                // Последняя "нулевая" страница PDFium или сбой рендера — пропускаем.
                continue;
            }

            if (raw is null || raw.Length == 0 || width <= 0 || height <= 0)
            {
                continue;
            }

            using var bmp = CreateBitmap(raw, width, height);
            using var pix = PixConverter.ToPix(bmp);
            using var page = engine.Process(pix);
            var text = page.GetText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Append(text);
                result.AppendLine();
            }
        }

        return result.ToString();
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