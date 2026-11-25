using PdfShark.Models;
using System.Text.Json;

namespace PdfShark.Services;

/// <summary>
/// Простой JSON-кэш результатов распознавания. Позволяет не перераспознавать
/// документ при каждом обновлении, если файл не менялся.
/// </summary>
public sealed class DocumentCache
{
    private sealed record CacheEntry(
        long ModifiedUtcTicks,
        string Theme,
        string Summary,
        DocumentStatus Status);

    private readonly string _cacheFile;
    private readonly Dictionary<string, CacheEntry> _entries;

    public DocumentCache()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PdfShark");
        Directory.CreateDirectory(dir);
        _cacheFile = Path.Combine(dir, "cache.json");
        _entries = Load();
    }

    private Dictionary<string, CacheEntry> Load()
    {
        try
        {
            if (File.Exists(_cacheFile))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var data = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(
                    File.ReadAllText(_cacheFile), options);
                return data ?? new Dictionary<string, CacheEntry>();
            }
        }
        catch
        {
            // Повреждённый кэш — начинаем заново.
        }
        return new Dictionary<string, CacheEntry>();
    }

    /// <summary>Пытается получить закэшированный результат для неизменённого файла.</summary>
    public bool TryGet(string fullPath, DateTime lastWriteUtc, out DocumentInfo doc)
    {
        string key = NormalizeKey(fullPath);
        if (_entries.TryGetValue(key, out var entry) &&
            entry.ModifiedUtcTicks == lastWriteUtc.Ticks)
        {
            var fi = new FileInfo(fullPath);
            doc = new DocumentInfo
            {
                FileName = fi.Name,
                FullPath = fullPath,
                Modified = fi.LastWriteTime,
                Theme = entry.Theme,
                Summary = entry.Summary,
                Status = entry.Status
            };
            return true;
        }
        doc = null!;
        return false;
    }

    /// <summary>Сохраняет результат обработки в кэш.</summary>
    public void Set(DocumentInfo doc)
    {
        _entries[NormalizeKey(doc.FullPath)] = new CacheEntry(
            doc.Modified.Ticks,
            doc.Theme,
            doc.Summary,
            doc.Status);
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries);
            File.WriteAllText(_cacheFile, json);
        }
        catch
        {
            // Кэш не критичен — при ошибке просто не сохраняем.
        }
    }

    private static string NormalizeKey(string fullPath)
    {
        return fullPath.ToLowerInvariant();
    }
}