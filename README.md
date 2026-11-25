# PDF-SHAR

Десктопное приложение на **C# / WinForms** (только для Windows). Сканирует папку со
сканированными документами (PDF), распознаёт их (текстовый слой + OCR), определяет
«тему» и краткое содержание и показывает список в отдельном окне.

## Возможности

- 📂 Выбор любой папки с PDF-документами.
- 🧠 Распознавание:
  - извлечение готового текстового слоя PDF (PdfPig);
  - OCR сканов без текста (Tesseract) через рендеринг страниц PDFium (Docnet.Core);
  - языки **русский + английский**.
- 🏷 Простая «тема» и краткое содержание: частотный анализ слов + выбор самых
  «весомых» предложений (без внешних сервисов и ключей API).
- 🔄 Обновление списка:
  - кнопка «Обновить»;
  - автообновление раз в час;
  - автоматически при изменении файлов в папке.
- ✨ Открытие документа по двойному щелчку / кнопке «Открыть».
- ⚙️ Кэш результатов (не перераспознаёт неизменённые файлы).

## Сборка и запуск (Windows)

```bash
dotnet build -c Release
dotnet run -c Release
```

### Публикация под Windows

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

После публикации в папке вывода появится `PDF-shar.exe`.

> Сборка возможна и из-под Linux (кросс-компиляция задаётся через
> `EnableWindowsTargeting` в `PDF-shar.csproj`), но приложение рассчитано на запуск
> только в Windows.

## Подготовка данных OCR (важно!)

Для распознавания сканов приложению нужны языковые данные Tesseract. Положите
папку **`tessdata`** рядом с `PDF-shar.exe`, содержащую:

- `eng.traineddata`
- `rus.traineddata`

Скачать можно из репозитория
[tesseract-ocr/tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast)
(например, https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata).

Если данных OCR нет, документы с текстовым слоем обрабатываются, а сканы получат
статус «Нет данных OCR».

## Требования

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
  (для self-contained публикации — не требуется)

## Структура проекта

```
Forms/MainForm.cs                  — главное окно (WinForms)
Models/DocumentInfo.cs             — модель документа
Models/DocumentStatus.cs           — статусы обработки
Services/PdfTextExtractor.cs       — извлечение текста из PDF (PdfPig)
Services/OcrService.cs             — OCR сканов (PDFium + Tesseract)
Services/SimpleTextAnalyzer.cs     — тема и краткое содержание
Services/FolderDocumentProcessor.cs— координация: сканирование + распознавание
Services/DocumentCache.cs          — JSON-кэш результатов
Program.cs                         — точка входа
```