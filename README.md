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

### Собрать self-contained версию (для запуска на любом Windows-ПК без .NET)

> ⚠️ **Не публикуйте в один файл (single-file)!** Движок OCR Tesseract находит свои
> нативные DLL по реальному пути сборки; в single-file этого пути нет и OCR падает.
> Надёжный вариант — self-contained **папка**.

```bash
# Windows:
.\tools\publish-win.ps1
# или Linux:
bash tools/publish-win.sh
```

Результат — архив **`publish/PDF-shar-win-x64.zip`**. Распакуйте на любом
Windows-ПК (10/11) в любую папку и запустите **`PDF-shar.exe`** — .NET и OCR-данные
встроены, отдельная установка не нужна. В папке рядом с exe всегда должны лежать
нативные библиотеки: `pdfium.dll` и `x64/tesseract50.dll` (они идут в комплекте).

> **Visual C++ Runtime в комплекте.** `tesseract50.dll` требует MSVCP/VCRUNTIME
> (`msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll` и др.). Они вкладываются
> скриптом из `runtime/vcrt/` в корень и в `x64/`, поэтому OCR работает на ПК даже
> без установленного «Microsoft Visual C++ 2015–2022 Redistributable». Источник DLL —
> официальный `vc_redist.x64.exe`.

> Сборка возможна и из-под Linux (кросс-компиляция задаётся через
> `EnableWindowsTargeting` в `PDF-shar.csproj`), но приложение рассчитано на запуск
> только в Windows.

## Данные OCR

Языковые данные Tesseract (**eng + rus**, `tessdata_fast`) **встроены прямо в EXE** —
дополнительно ничего скачивать и класть рядом не нужно. При первом запуске они
автоматически извлекаются в `%LocalAppData%\PdfShark\tessdata`.

> Если рядом с `PDF-shar.exe` всё же положить собственную папку `tessdata`
> (с `eng.traineddata` и `rus.traineddata`), приложение будет использовать её.

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