namespace PdfShark.Models;

/// <summary>Метаданные одного документа, отображаемые в таблице.</summary>
public sealed class DocumentInfo
{
    /// <summary>Имя файла (без пути).</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Полный путь к файлу.</summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>Время последнего изменения файла.</summary>
    public DateTime Modified { get; init; }

    /// <summary>Тема документа (самый простой способ — частотный анализ).</summary>
    public string Theme { get; set; } = string.Empty;

    /// <summary>Краткое содержание (2–3 предложения).</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Найденный/распознанный текст (используется анализатором).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Статус обработки.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Queued;

    /// <summary>Отображаемый текст статуса.</summary>
    public string StatusText => Status switch
    {
        DocumentStatus.Queued => "В очереди",
        DocumentStatus.Processing => "Обработка...",
        DocumentStatus.Ready => "Готово",
        DocumentStatus.NotPdf => "Не PDF",
        DocumentStatus.Error => "Ошибка",
        DocumentStatus.NoText => "Нет текста",
        DocumentStatus.OcrDataMissing => "Нет данных OCR",
        _ => Status.ToString()
    };
}