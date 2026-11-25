using System.Runtime.InteropServices;
using PdfShark.Services;

namespace PdfShark;

/// <summary>Точка входа десктопного приложения.</summary>
internal static class Program
{
    /// <summary>Задаёт каталог поиска неуправляемых библиотек (для нативных DLL OCR).</summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string? lpPathName);

    /// <summary>
    /// Главная точка входа. WinForms требует [STAThread].
    /// </summary>
    [STAThread]
    private static void Main()
    {
        AppLog.Initialize();
        PrepareNativeLibraries();

        // Глобальная диагностика: все необработанные исключения пишем в лог.
        Application.ThreadException += (_, e) =>
            AppLog.Error("Необработанное исключение в UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Критическое исключение",
                e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new Forms.MainForm());
        }
        catch (Exception ex)
        {
            AppLog.Error("Ошибка во время работы приложения", ex);
            throw;
        }
    }

    /// <summary>
    /// В single-file режиме нативные библиотеки (pdfium.dll, x64/tesseract50.dll,
    /// leptonica) находятся в AppContext.BaseDirectory (каталог извлечения).
    /// Добавляем их в каталог поиска DLL и в PATH, чтобы Docnet/Tesseract их нашли.
    /// </summary>
    private static void PrepareNativeLibraries()
    {
        string baseDir = AppContext.BaseDirectory;
        var dirs = new List<string>();
        if (!string.IsNullOrEmpty(baseDir))
        {
            dirs.Add(baseDir);
            string x64 = Path.Combine(baseDir, "x64");
            if (Directory.Exists(x64))
            {
                dirs.Add(x64);
            }
        }

        // Каталог поиска неуправляемых DLL.
        if (dirs.Count > 0 && !string.IsNullOrEmpty(baseDir))
        {
            try
            {
                SetDllDirectory(baseDir);
            }
            catch
            {
                // Не критично: дублируем через PATH.
            }
        }

        // Запасной механизм — PATH.
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        bool changed = false;
        foreach (string d in dirs)
        {
            if (path.Contains(d, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            path = d + ";" + path;
            changed = true;
        }
        if (changed)
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }
    }
}