namespace PdfShark;

/// <summary>Точка входа десктопного приложения.</summary>
internal static class Program
{
    /// <summary>
    /// Главная точка входа. WinForms требует [STAThread].
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Forms.MainForm());
    }
}