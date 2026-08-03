using System.Text.Json;
using ViewMd.Models;

namespace ViewMd.Services;

public sealed class MruService
{
    private const int MaxEntries = 15;

    private readonly string _path;
    private MruStore _store;

    public MruService(string? path = null)
    {
        _path = path ?? AppPaths.MruFilePath;
        _store = Load(_path);
    }

    public IReadOnlyList<RecentEntry> Files => _store.Files;
    public IReadOnlyList<RecentEntry> Folders => _store.Folders;

    public void RecordFile(string fullPath) => Record(_store.Files, fullPath, RecentEntryKind.File);

    public void RecordFolder(string fullPath) => Record(_store.Folders, fullPath, RecentEntryKind.Folder);

    private void Record(List<RecentEntry> list, string fullPath, RecentEntryKind kind)
    {
        list.RemoveAll(e => string.Equals(e.Path, fullPath, StringComparison.Ordinal));
        list.Insert(0, new RecentEntry { Path = fullPath, Kind = kind, LastOpened = DateTimeOffset.UtcNow });
        if (list.Count > MaxEntries)
        {
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        Save();
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_store, AppJsonContext.Default.MruStore);
            File.WriteAllText(_path, json);
        }
        catch (IOException)
        {
            // Best-effort persistence; losing the MRU list on a write failure isn't fatal.
        }
    }

    private static MruStore Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var store = JsonSerializer.Deserialize(json, AppJsonContext.Default.MruStore);
                if (store is not null)
                {
                    return store;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }

        return new MruStore();
    }
}
