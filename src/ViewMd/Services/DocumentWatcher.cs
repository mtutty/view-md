namespace ViewMd.Services;

// Watches either a single Markdown file, or a whole folder plus one "active" file
// within it, and raises debounced callbacks so editors that write in multiple
// small operations don't cause re-render/tree-refresh thrashing.
public sealed class DocumentWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(250);

    private FileSystemWatcher? _watcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly object _gate = new();
    private string? _activeFilePath;

    public event Action? FileChanged;
    public event Action? FolderChanged;

    // No folder is open — just watch one file for content changes.
    public void WatchFile(string filePath)
    {
        Stop();
        _activeFilePath = filePath;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => Debounce(() => FileChanged?.Invoke());
        _watcher.EnableRaisingEvents = true;
    }

    // A folder is open: watch the whole tree recursively for structural changes,
    // and additionally re-render if the currently displayed file's content changes.
    public void WatchFolder(string folderPath, string? activeFilePath)
    {
        Stop();
        _activeFilePath = activeFilePath;

        _watcher = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
        };
        _watcher.Created += (_, _) => Debounce(() => FolderChanged?.Invoke());
        _watcher.Deleted += (_, _) => Debounce(() => FolderChanged?.Invoke());
        _watcher.Renamed += (_, _) => Debounce(() => FolderChanged?.Invoke());
        _watcher.Changed += (_, e) =>
        {
            if (_activeFilePath is not null && string.Equals(e.FullPath, _activeFilePath, StringComparison.Ordinal))
            {
                Debounce(() => FileChanged?.Invoke());
            }
        };
        _watcher.EnableRaisingEvents = true;
    }

    // Updates which file's Changed events should trigger a re-render, without
    // tearing down and rebuilding the recursive folder watcher.
    public void SetActiveFile(string? filePath) => _activeFilePath = filePath;

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    private void Debounce(Action callback)
    {
        lock (_gate)
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Timers.Timer(DebounceInterval.TotalMilliseconds) { AutoReset = false };
            _debounceTimer.Elapsed += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(callback);
            };
            _debounceTimer.Start();
        }
    }

    public void Dispose()
    {
        Stop();
        _debounceTimer?.Dispose();
    }
}
