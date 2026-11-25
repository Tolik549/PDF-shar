namespace PdfShark.Models;

/// <summary>Статус обработки документа.</summary>
public enum DocumentStatus
{
    /// <summary>Документ ждёт обработки.</summary>
    Queued,

    /// <summary>Документ распознаётся/анализируется прямо сейчас.</summary>
    Processing,

    /// <summary>Документ успешно распознан и проанализирован.</summary>
    Ready,

    /// <summary>Файл не является PDF или произошла ошибка чтения.</summary>
    NotPdf,

    /// <summary>Ошибка при извлечении текста/OCR.</summary>
    Error,

    /// <summary>Не удалось извлечь текст (возможно, пустой/графический документ).</summary>
    NoText,

    /// <summary>Отсутствуют файлы языковых данных OCR (tessdata).</summary>
    OcrDataMissing
}