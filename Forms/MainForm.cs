using PdfShark.Models;
using PdfShark.Services;
using System.ComponentModel;
using System.Diagnostics;

namespace PdfShark.Forms;

/// <summary>
/// Главное окно: выбор папки, таблица документов (имя / тема / краткое содержание),
/// ручное обновление, автообновление раз в час и отслеживание изменений файлов.
/// </summary>
public sealed class MainForm : Form
{
    private const string SettingsKeyPath = @"Software\PdfShark";
    private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromHours(1);

    private readonly FolderDocumentProcessor _processor = new();
    private readonly List<DocumentInfo> _documents = new();
    private CancellationTokenSource? _cts;

    // UI-элементы
    private TextBox _folderBox = null!;
    private Button _browseButton = null!;
    private Button _refreshButton = null!;
    private Button _openButton = null!;
    private Button _showErrorButton = null!;
    private Button _logButton = null!;
    private DataGridView _grid = null!;
    private ToolStripStatusLabel _statusLabel = null!;

    // Обновления
    private System.Windows.Forms.Timer _autoRefreshTimer = null!;
    private System.Windows.Forms.Timer _debounceTimer = null!;
    private FileSystemWatcher _watcher = null!;
    private bool _refreshing;

    public MainForm()
    {
        Text = "PDF-SHAR — сканер документов";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(980, 560);
        BuildUi();
        _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = (int)AutoRefreshInterval.TotalMilliseconds };
        _autoRefreshTimer.Tick += (_, _) => BeginRefresh();
        _debounceTimer = new System.Windows.Forms.Timer { Interval = 900 };
        _debounceTimer.Tick += (_, _) => BeginRefresh();
        Load += OnLoaded;
        FormClosing += OnClosing;
    }

    private void BuildUi()
    {
        // ---- Верхняя панель: путь + кнопки ----
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 10, 10, 4),
            WrapContents = false
        };

        _folderBox = new TextBox { Width = 520 };
        _browseButton = new Button { Text = "Выбрать...", AutoSize = true };
        _refreshButton = new Button { Text = "Обновить", AutoSize = true };
        _openButton = new Button { Text = "Открыть", AutoSize = true };
        _showErrorButton = new Button { Text = "Показать ошибку", AutoSize = true };
        _logButton = new Button { Text = "Лог", AutoSize = true };

        _browseButton.Click += (_, _) => BrowseFolder();
        _refreshButton.Click += (_, _) => BeginRefresh();
        _openButton.Click += (_, _) => OpenSelected();
        _showErrorButton.Click += (_, _) => ShowSelectedError();
        _logButton.Click += (_, _) => OpenLog();

        topPanel.Controls.Add(new Label { Text = "Папка:", AutoSize = true, Anchor = AnchorStyles.Left });
        topPanel.Controls.Add(_folderBox);
        topPanel.Controls.Add(_browseButton);
        topPanel.Controls.Add(_refreshButton);
        topPanel.Controls.Add(_openButton);
        topPanel.Controls.Add(_showErrorButton);
        topPanel.Controls.Add(_logButton);

        // ---- Таблица ----
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoGenerateColumns = false,
            RowHeadersVisible = false
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.FileName), HeaderText = "Файл", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.Theme), HeaderText = "Тема", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 25 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.Summary), HeaderText = "Краткое содержание", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.StatusText), HeaderText = "Статус", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.Error), HeaderText = "Ошибка", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DocumentInfo.Modified), HeaderText = "Изменено", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 14 });
        _grid.CellDoubleClick += (_, _) => OpenSelected();

        // ---- Статусная строка ----
        var statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Готов к работе");

        Controls.Add(_grid);
        Controls.Add(topPanel);
        statusStrip.Items.Add(_statusLabel);
        Controls.Add(statusStrip);
    }
private void OnLoaded(object? sender, EventArgs e)
    {
        // Восстанавливаем последнюю выбранную папку.
        string? lastFolder = ReadSetting("LastFolder");
        if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
        {
            _folderBox.Text = lastFolder;
            StartWatcher(lastFolder);
            BeginRefresh();
        }
        _autoRefreshTimer.Start();
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_folderBox.Text))
        {
            WriteSetting("LastFolder", _folderBox.Text);
        }
        _autoRefreshTimer.Stop();
        _debounceTimer.Stop();
        _cts?.Cancel();
        _watcher?.Dispose();
        _processor.Dispose();
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку со сканированными документами (PDF)",
            UseDescriptionForTitle = true,
            SelectedPath = _folderBox.Text
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _folderBox.Text = dialog.SelectedPath;
        StartWatcher(dialog.SelectedPath);
        BeginRefresh();
    }

    private void StartWatcher(string folder)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(folder)
        {
            Filter = "*.pdf",
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        void OnChange(object _, FileSystemEventArgs e) => ScheduleRefresh();
        _watcher.Created += OnChange;
        _watcher.Changed += OnChange;
        _watcher.Renamed += OnChange;
        _watcher.Deleted += OnChange;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>При изменении файлов ждём паузу и обновляем список (смена на UI-поток).</summary>
    private void ScheduleRefresh()
    {
        BeginInvoke(() =>
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        });
    }
private async void BeginRefresh()
    {
        string folder = _folderBox.Text.Trim();
        if (_refreshing)
        {
            return;
        }
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            _statusLabel.Text = "Укажите существующую папку.";
            return;
        }

        _refreshing = true;
        _refreshButton.Enabled = false;
        _statusLabel.Text = "Сканирование началось...";

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        CancellationToken ct = _cts.Token;

        var progress = new Progress<string>(s => _statusLabel.Text = s);

        try
        {
            IReadOnlyList<DocumentInfo> result = await Task.Run(
                () => _processor.RefreshFolder(folder, progress, ct), ct);

            _documents.Clear();
            _documents.AddRange(result);
            BindGrid();
            _statusLabel.Text = $"Готово: {result.Count} док. (обновлено {DateTime.Now.ToShortTimeString()})";

            if (!_processor.HasOcrData && result.Any(d => d.Status == DocumentStatus.OcrDataMissing))
            {
                _statusLabel.Text += "  •  нет OCR-данных: положите tessdata рядом с приложением.";
            }
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Сканирование отменено.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка: " + ex.Message;
        }
        finally
        {
            _refreshing = false;
            _refreshButton.Enabled = true;
        }
    }

    private void BindGrid()
    {
        _grid.DataSource = new BindingList<DocumentInfo>(_documents);
        _grid.ClearSelection();
    }

    private void OpenSelected()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }
        if (_grid.SelectedRows[0].DataBoundItem is not DocumentInfo doc)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = doc.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось открыть документ: " + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowSelectedError()
    {
        if (_grid.SelectedRows.Count == 0)
        {
            return;
        }
        if (_grid.SelectedRows[0].DataBoundItem is not DocumentInfo doc)
        {
            return;
        }

        string text = string.IsNullOrEmpty(doc.Error)
            ? "Ошибок распознавания для этого документа нет."
            : doc.Error;

        MessageBox.Show(this, text,
            "Диагностика: " + (string.IsNullOrEmpty(doc.Error) ? doc.FileName : doc.FileName),
            MessageBoxButtons.OK,
            string.IsNullOrEmpty(doc.Error) ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void OpenLog()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Services.AppLog.LogFilePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Не удалось открыть лог.\nФайл: " + Services.AppLog.LogFilePath + "\n\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

private static string? ReadSetting(string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
            return key?.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteSetting(string name, string value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SettingsKeyPath);
            key?.SetValue(name, value);
        }
        catch
        {
            // Настройки не критичны.
        }
    }
}