using PdfPig = UglyToad.PdfPig;

namespace PdfShark.Services;

/// <summary>Извлекает текст из PDF-файла.</summary>
public interface IPdfTextExtractor
{
    /// <summary>Возвращает текстовый слой PDF (пустая строка, если его нет).</summary>
    string ExtractText(string pdfPath);
}

/// <summary>Извлечение текстового слоя PDF через PdfPig.</summary>
public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string ExtractText(string pdfPath)
    {
        var sb = new System.Text.StringBuilder();
        using (var doc = PdfPig.PdfDocument.Open(pdfPath))
        {
            foreach (var page in doc.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append(text);
                    sb.AppendLine();
                }
            }
        }
        return sb.ToString();
    }
}