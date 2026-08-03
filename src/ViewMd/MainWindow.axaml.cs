using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ViewMd.Models;
using ViewMd.Rendering;
using ViewMd.Services;

namespace ViewMd;

public partial class MainWindow : Window
{
    private readonly MruService _mru;
    private readonly SettingsService _settings;
    private readonly DocumentWatcher _watcher;
    private readonly DocumentSearch _search;

    private Control? _currentDocumentRoot;
    private string? _currentFilePath;
    private string? _currentFolderPath;
    private bool _sidebarVisible = true;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? initialPath)
    {
        InitializeComponent();

        _mru = new MruService();
        _settings = new SettingsService();
        _watcher = new DocumentWatcher();
        _search = new DocumentSearch();

        Width = _settings.Current.WindowWidth;
        Height = _settings.Current.WindowHeight;

        SetSidebarVisible(_settings.Current.SidebarVisible);

        _watcher.FileChanged += () =>
        {
            if (_currentFilePath is not null)
            {
                OpenFile(_currentFilePath, recordMru: false);
            }
        };
        _watcher.FolderChanged += () =>
        {
            if (_currentFolderPath is not null)
            {
                RefreshTree(_currentFolderPath);
            }
        };

        AddHandler(KeyDownEvent, OnWindowKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closing += OnWindowClosing;

        RefreshRecentMenus();
        ShowEmptyState();

        if (initialPath is not null)
        {
            OpenPath(initialPath);
        }
    }

    public void OpenPath(string path)
    {
        if (Directory.Exists(path))
        {
            OpenFolder(path);
        }
        else if (File.Exists(path))
        {
            OpenFile(path);
        }
    }

    private void OpenFile(string path, bool recordMru = true)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            ShowError($"Couldn't open {Path.GetFileName(path)}: {ex.Message}");
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            ShowError($"Couldn't open {Path.GetFileName(path)}: {ex.Message}");
            return;
        }

        var baseDir = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        var renderer = new MarkdownRenderer(baseDir, OnLinkActivated);
        var rendered = renderer.Render(text);

        DocumentHost.Content = rendered;
        _currentDocumentRoot = rendered;
        _currentFilePath = path;
        Title = $"{Path.GetFileName(path)} — view-md";
        ExportPdfMenuItem.IsEnabled = true;
        RevealMenuItem.IsEnabled = true;

        if (_currentFolderPath is not null && IsUnderDirectory(path, _currentFolderPath))
        {
            _watcher.SetActiveFile(path);
        }
        else
        {
            _watcher.WatchFile(path);
        }

        if (recordMru)
        {
            _mru.RecordFile(path);
            RefreshRecentMenus();
        }

        if (SearchBarPanel.IsVisible)
        {
            RunSearch(SearchBox.Text);
        }
    }

    private void OpenFolder(string path, bool recordMru = true)
    {
        _currentFolderPath = path;
        RefreshTree(path);
        SetSidebarVisible(true);
        _watcher.WatchFolder(path, _currentFilePath);

        if (recordMru)
        {
            _mru.RecordFolder(path);
            RefreshRecentMenus();
        }
    }

    private void RefreshTree(string folderPath)
    {
        // Built as plain TreeViewItems (Tag = MarkdownTreeNode) rather than via a
        // hierarchical DataTemplate: this Avalonia version only ships the generic
        // FuncTreeDataTemplate<T>, not a XAML-friendly TreeDataTemplate.
        var root = MarkdownFileTreeBuilder.Build(folderPath);
        SidebarTreeView.ItemsSource = root is null ? Array.Empty<TreeViewItem>() : new[] { BuildTreeViewItem(root) };
    }

    private static TreeViewItem BuildTreeViewItem(MarkdownTreeNode node)
    {
        var item = new TreeViewItem { Header = node.Name, Tag = node, IsExpanded = node.IsDirectory };
        foreach (var child in node.Children)
        {
            item.Items.Add(BuildTreeViewItem(child));
        }

        return item;
    }

    private void ShowEmptyState()
    {
        DocumentHost.Content = new TextBlock
        {
            Text = "Open a Markdown file (Ctrl+O) or a folder (Ctrl+Shift+O) to get started.",
            Margin = new Thickness(20),
            Opacity = 0.6,
        };
        _currentDocumentRoot = null;
    }

    private void OnLinkActivated(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var isExternal = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto";

        if (!isExternal)
        {
            var baseDir = _currentFilePath is not null ? Path.GetDirectoryName(_currentFilePath) : _currentFolderPath;
            if (baseDir is not null)
            {
                var candidate = Path.GetFullPath(Path.Combine(baseDir, url));
                if (File.Exists(candidate))
                {
                    var ext = Path.GetExtension(candidate);
                    if (string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".markdown", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenFile(candidate);
                    }
                    else
                    {
                        OpenExternally(candidate);
                    }

                    return;
                }
            }
        }

        OpenExternally(url);
    }

    private static void OpenExternally(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Best-effort: no OS handler for this target. Nothing sensible to surface
            // beyond swallowing it, the same way a browser fails silently on a bad link.
        }
    }

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        var fullFile = Path.GetFullPath(filePath);
        var fullDir = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullFile.StartsWith(fullDir, StringComparison.Ordinal);
    }

    private void SetSidebarVisible(bool visible)
    {
        _sidebarVisible = visible;
        SidebarPanel.IsVisible = visible;
        SidebarSplitter.IsVisible = visible;
        BodyGrid.ColumnDefinitions[0].Width = visible
            ? new GridLength(_settings.Current.SidebarWidth > 0 ? _settings.Current.SidebarWidth : 260)
            : new GridLength(0);
    }

    private void RefreshRecentMenus()
    {
        BuildRecentMenu(RecentFilesMenu, _mru.Files);
        BuildRecentMenu(RecentFoldersMenu, _mru.Folders);
    }

    private void BuildRecentMenu(MenuItem parent, IReadOnlyList<RecentEntry> entries)
    {
        parent.Items.Clear();
        if (entries.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }

        foreach (var entry in entries)
        {
            var item = new MenuItem { Header = entry.Path };
            item.Click += (_, _) => OpenPath(entry.Path);
            parent.Items.Add(item);
        }
    }

    private void ShowError(string message)
    {
        var okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var dialog = new Window
        {
            Title = "view-md",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, okButton },
            },
        };
        okButton.Click += (_, _) => dialog.Close();
        _ = dialog.ShowDialog(this);
    }

    // --- Search ---

    private void RunSearch(string? query)
    {
        if (_currentDocumentRoot is null)
        {
            UpdateMatchCountText();
            return;
        }

        _search.Reset(_currentDocumentRoot, query ?? string.Empty);
        if (_search.MatchCount > 0)
        {
            ScrollToMatch(_search.Next());
        }

        UpdateMatchCountText();
    }

    private void ScrollToMatch(TextBlock? match)
    {
        if (match is null)
        {
            return;
        }

        var transform = match.TransformToVisual(ContentScrollViewer);
        if (transform is { } m)
        {
            var point = m.Transform(new Point(0, 0));
            var targetY = Math.Max(0, ContentScrollViewer.Offset.Y + point.Y - 60);
            ContentScrollViewer.Offset = new Vector(ContentScrollViewer.Offset.X, targetY);
        }
    }

    private void UpdateMatchCountText()
    {
        SearchMatchCountText.Text = _search.MatchCount == 0 ? "0 / 0" : $"{_search.CurrentMatchNumber} / {_search.MatchCount}";
    }

    // --- Event handlers (wired from MainWindow.axaml) ---

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown File",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Markdown") { Patterns = ["*.md", "*.markdown"] }],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            OpenPath(path);
        }
    }

    private async void OnOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            OpenFolder(path);
        }
    }

    private async void OnExportPdfClick(object? sender, RoutedEventArgs e)
    {
        if (_currentDocumentRoot is null || _currentFilePath is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export to PDF",
            SuggestedFileName = Path.GetFileNameWithoutExtension(_currentFilePath) + ".pdf",
            FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
        });

        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        try
        {
            PdfExportService.Export(_currentDocumentRoot, path);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            ShowError($"Export failed: {ex.Message}");
        }
    }

    private void OnRevealInSidebarClick(object? sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null)
        {
            return;
        }

        var parent = Path.GetDirectoryName(_currentFilePath);
        if (parent is not null)
        {
            OpenFolder(parent, recordMru: false);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnToggleSidebarClick(object? sender, RoutedEventArgs e) => SetSidebarVisible(!_sidebarVisible);

    private void OnSidebarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewItem { Tag: MarkdownTreeNode { IsDirectory: false } node })
        {
            // Selecting individual files within an open folder doesn't push a new MRU
            // entry — only the folder root does. See .charter/capabilities/mru-list.md.
            OpenFile(node.FullPath, recordMru: false);
        }
    }

    private void OnFindClick(object? sender, RoutedEventArgs e)
    {
        SearchBarPanel.IsVisible = true;
        SearchBox.Focus();
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            RunSearch(SearchBox.Text);
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) => RunSearch(SearchBox.Text);

    private void OnSearchNextClick(object? sender, RoutedEventArgs e)
    {
        ScrollToMatch(_search.Next());
        UpdateMatchCountText();
    }

    private void OnSearchPrevClick(object? sender, RoutedEventArgs e)
    {
        ScrollToMatch(_search.Previous());
        UpdateMatchCountText();
    }

    private void OnSearchCloseClick(object? sender, RoutedEventArgs e)
    {
        SearchBarPanel.IsVisible = false;
        _search.Reset(_currentDocumentRoot ?? DocumentHost, string.Empty);
        UpdateMatchCountText();
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                OnSearchPrevClick(sender, e);
            }
            else
            {
                OnSearchNextClick(sender, e);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            OnSearchCloseClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && shift && e.Key == Key.O)
        {
            OnOpenFolderClick(sender, e);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.O)
        {
            OnOpenFileClick(sender, e);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.F)
        {
            OnFindClick(sender, e);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.B)
        {
            OnToggleSidebarClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBarPanel.IsVisible)
        {
            OnSearchCloseClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            if (shift)
            {
                OnSearchPrevClick(sender, e);
            }
            else
            {
                OnSearchNextClick(sender, e);
            }

            e.Handled = true;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _settings.Current.WindowWidth = Width;
        _settings.Current.WindowHeight = Height;
        _settings.Current.SidebarVisible = _sidebarVisible;
        var sidebarWidth = BodyGrid.ColumnDefinitions[0].Width;
        if (sidebarWidth.IsAbsolute)
        {
            _settings.Current.SidebarWidth = sidebarWidth.Value;
        }

        _settings.Save();
        _watcher.Dispose();
    }
}
