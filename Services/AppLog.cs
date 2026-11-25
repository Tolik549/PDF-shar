using System.Text;

namespace PdfShark.Services;

/// <summary>
/// Простой файловый лог ошибок диагностики.
/// Файл: %LocalAppData%\PdfShark\log.txt
/// </summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static string? _path;

    /// <summary>Полный путь к файлу лога.</summary>
    public static string LogFilePath
    {
        get
        {
            EnsureDirectory();
            return _path!;
        }
    }

    public static void Initialize()
    {
        EnsureDirectory();
    }

    private static void EnsureDirectory()
    {
        if (_path is not null)
        {
            return;
        }
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfShark");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "log.txt");
    }

    public static void Info(string message)
    {
        Append($"[INFO] {message}");
    }

    public static void Error(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[ERROR] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}");
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            sb.AppendLine($"  -- {e.GetType().FullName}: {e.Message}");
            sb.AppendLine($"     {e.StackTrace}");
        }
        Append(sb.ToString());
    }

    private static void Append(string text)
    {
        try
        {
            lock (Sync)
            {
                EnsureDirectory();
                File.AppendAllText(_path!, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            }
        }
        catch
        {
            // Логирование не должно ломать приложение.
        }
    }
}