using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace ViewMd.Rendering;

// Find-in-page over the currently rendered document. Matches are highlighted at
// paragraph/block granularity (the containing TextBlock), not as an exact
// character-range highlight — see .charter/capabilities/search-in-file.md for why
// that's an acceptable v1 simplification.
public sealed class DocumentSearch
{
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(90, 255, 215, 0));

    private List<TextBlock> _matches = [];
    private int _currentIndex = -1;
    private TextBlock? _highlighted;

    public int MatchCount => _matches.Count;
    public int CurrentMatchNumber => _currentIndex + 1;

    public void Reset(Control documentRoot, string query)
    {
        ClearHighlight();
        _matches.Clear();
        _currentIndex = -1;

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        foreach (var descendant in documentRoot.GetLogicalDescendants())
        {
            if (descendant is TextBlock { } tb && tb.Inlines is not null)
            {
                var text = tb.Inlines.Text ?? string.Empty;
                if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    _matches.Add(tb);
                }
            }
        }
    }

    public TextBlock? Next()
    {
        if (_matches.Count == 0)
        {
            return null;
        }

        _currentIndex = (_currentIndex + 1) % _matches.Count;
        return Highlight();
    }

    public TextBlock? Previous()
    {
        if (_matches.Count == 0)
        {
            return null;
        }

        _currentIndex = (_currentIndex - 1 + _matches.Count) % _matches.Count;
        return Highlight();
    }

    private TextBlock Highlight()
    {
        ClearHighlight();
        var match = _matches[_currentIndex];
        match.Background = HighlightBrush;
        _highlighted = match;
        return match;
    }

    private void ClearHighlight()
    {
        if (_highlighted is not null)
        {
            _highlighted.Background = null;
            _highlighted = null;
        }
    }
}
