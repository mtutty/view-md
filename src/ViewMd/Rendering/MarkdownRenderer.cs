using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;

namespace ViewMd.Rendering;

// Walks a Markdig AST and emits Avalonia controls directly (no webview, no HTML
// intermediate step) — see .charter/decisions.md for why this is hand-rolled
// instead of a third-party Avalonia Markdown renderer package.
public sealed class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly string _baseDirectory;
    private readonly Action<string> _onLinkActivated;
    private readonly MarkdownRenderOptions _options;
    private readonly Dictionary<string, Control> _anchors = new(StringComparer.OrdinalIgnoreCase);

    public MarkdownRenderer(string baseDirectory, Action<string> onLinkActivated, MarkdownRenderOptions options)
    {
        _baseDirectory = baseDirectory;
        _onLinkActivated = onLinkActivated;
        _options = options;
    }

    // Heading-id (GitHub-style slug, from Markdig's AutoIdentifier extension) -> the
    // rendered heading control, so same-page and cross-page "#anchor" links can scroll
    // to it instead of falling through to file/process resolution.
    public IReadOnlyDictionary<string, Control> Anchors => _anchors;

    public Control Render(string markdownText)
    {
        var document = Markdown.Parse(markdownText, Pipeline);

        var root = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4,
            Margin = new Avalonia.Thickness(_options.DocumentMargin),
        };

        foreach (var block in document)
        {
            var control = RenderBlock(block);
            if (control is not null)
            {
                root.Children.Add(control);
            }
        }

        return root;
    }

    private Control? RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                return RenderHeading(heading);
            case ParagraphBlock paragraph:
                // A paragraph that's *only* an image — by far the common case for
                // "![alt](url)" on its own line — is rendered as a plain block-level
                // control, sibling to headings/paragraphs in the root StackPanel,
                // rather than embedded via InlineUIContainer inside a TextBlock like
                // an inline image would be. This isn't just style: an Image of any
                // real size (tested with a 288x288 PNG — the 1x1 test pixels used
                // earlier couldn't reveal this) embedded inline blows up the
                // TextBlock's line box and blanks the whole line, image and any
                // surrounding text alike. Block-level Image controls elsewhere in
                // this renderer (see RenderCodeBlock, RenderTable) don't have that
                // problem — only text-flow embedding does.
                return TryGetSoleImage(paragraph, out var soleImage)
                    ? RenderImage(soleImage)
                    : RenderParagraph(paragraph);
            case QuoteBlock quote:
                return RenderQuote(quote);
            case ListBlock list:
                return RenderList(list);
            case FencedCodeBlock fenced:
                return RenderCodeBlock(fenced);
            case CodeBlock code:
                return RenderCodeBlock(code);
            case ThematicBreakBlock:
                return new Border { Height = 1, Background = Brushes.Gray, Margin = new Avalonia.Thickness(0, 12), Opacity = 0.4 };
            case Table table:
                return RenderTable(table);
            case HtmlBlock html:
                return new TextBlock { Text = html.Lines.ToString(), FontFamily = "monospace", Opacity = 0.6, TextWrapping = TextWrapping.Wrap };
            default:
                // Unknown block kind: render nothing rather than throwing, so one
                // unsupported construct doesn't blank the whole document.
                return null;
        }
    }

    // True when a paragraph's only content is a single image — the common
    // "![alt](url)" on its own line — as opposed to an image mixed in with other
    // text/links in the same paragraph.
    private static bool TryGetSoleImage(ParagraphBlock paragraph, out LinkInline image)
    {
        if (paragraph.Inline is { FirstChild: LinkInline { IsImage: true } link } inline &&
            ReferenceEquals(inline.FirstChild, inline.LastChild))
        {
            image = link;
            return true;
        }

        image = null!;
        return false;
    }

    private Control RenderHeading(HeadingBlock heading)
    {
        // Ratios preserve the original fixed sizes (28/24/20/17/15/14) at the
        // default 14px base, while scaling with the user's configured base size.
        var ratio = heading.Level switch
        {
            1 => 2.0,
            2 => 1.7,
            3 => 1.4,
            4 => 1.2,
            5 => 1.05,
            _ => 1.0,
        };

        var text = new TextBlock
        {
            FontSize = _options.BaseFontSize * ratio,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(0, heading.Level == 1 ? 12 : 8, 0, 4),
        };
        ApplyFontFamily(text);
        var links = new List<(int Start, int End, string Url)>();
        var position = 0;
        AppendInlines(text.Inlines!, heading.Inline, links, ref position);
        AttachLinkHandling(text, links);

        var id = heading.GetAttributes().Id;
        if (!string.IsNullOrEmpty(id))
        {
            _anchors[id] = text;
        }

        return text;
    }

    private Control RenderParagraph(ParagraphBlock paragraph)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = _options.BaseFontSize,
            LineHeight = _options.BaseFontSize * _options.LineHeightMultiplier,
        };
        ApplyFontFamily(text);
        var links = new List<(int Start, int End, string Url)>();
        var position = 0;
        AppendInlines(text.Inlines!, paragraph.Inline, links, ref position);
        AttachLinkHandling(text, links);
        return text;
    }

    // Links render as plain in-flow Span/Run text (see AppendInline below) rather than
    // an embedded InlineUIContainer control, so they inherit the paragraph's FontSize,
    // LineHeight and baseline exactly like Bold/Italic do — no separate control to
    // fall out of alignment with the surrounding line. Run/Span aren't InputElements
    // in Avalonia, so clicking is done by hit-testing the parent TextBlock's
    // TextLayout; position is tracked to match exactly how InlineCollection.Text
    // assembles (Run = its text length, LineBreak = Environment.NewLine.Length,
    // embedded InlineUIContainer = 1 for its U+FFFC placeholder).
    private void AttachLinkHandling(TextBlock text, List<(int Start, int End, string Url)> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        // A TextBlock with no Background only hit-tests against painted glyph pixels,
        // not its full Bounds — so PointerPressed/PointerMoved would only fire when a
        // click lands exactly on ink (mostly missing whitespace between/around glyphs,
        // and glyph-sparse text like underscores or thin punctuation). Transparent
        // still paints nothing visually but makes the whole box a hit-test target.
        text.Background ??= Brushes.Transparent;

        var handCursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        var defaultCursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);

        string? LinkAt(Avalonia.Point point)
        {
            // TextLayout.HitTestPoint's point-to-character mapping only registers a hit
            // in a sliver of each wrapped line (confirmed empirically: everywhere below
            // the first visual line, all but ~0.25px of the line's height reports
            // "outside"), so clicks below the first line would silently miss. Testing
            // point-in-rect against each link's own HitTestTextRange rectangles (which
            // correctly span each line's full height) doesn't have that gap.
            foreach (var (start, end, url) in links)
            {
                foreach (var rect in text.TextLayout.HitTestTextRange(start, end - start))
                {
                    if (rect.Contains(point))
                    {
                        return url;
                    }
                }
            }

            return null;
        }

        text.PointerMoved += (_, e) => text.Cursor = LinkAt(e.GetPosition(text)) is not null ? handCursor : defaultCursor;
        text.PointerPressed += (_, e) =>
        {
            if (LinkAt(e.GetPosition(text)) is { } url)
            {
                _onLinkActivated(url);
            }
        };
    }

    private void ApplyFontFamily(TextBlock text)
    {
        if (!string.IsNullOrWhiteSpace(_options.FontFamily))
        {
            text.FontFamily = new FontFamily(_options.FontFamily);
        }
    }

    private Control RenderQuote(QuoteBlock quote)
    {
        var inner = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        foreach (var child in quote)
        {
            var control = RenderBlock(child);
            if (control is not null)
            {
                inner.Children.Add(control);
            }
        }

        return new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Avalonia.Thickness(4, 0, 0, 0),
            Padding = new Avalonia.Thickness(12, 2),
            Opacity = 0.85,
            Child = inner,
        };
    }

    private Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        var index = list.OrderedStart is not null && int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
            {
                continue;
            }

            panel.Children.Add(RenderListItem(listItem, list.IsOrdered, index));
            index++;
        }

        return panel;
    }

    private Control RenderListItem(ListItemBlock listItem, bool ordered, int index)
    {
        var isTask = TryGetTaskListMarker(listItem, out var isChecked);

        var marker = isTask
            ? (Control)new CheckBox { IsChecked = isChecked, IsEnabled = false, Margin = new Avalonia.Thickness(0, 1, 4, 0) }
            : new TextBlock
            {
                Text = ordered ? $"{index}." : "•",
                Width = 24,
                FontSize = _options.BaseFontSize,
                // Match the content paragraph's LineHeight so the marker's own
                // half-leading offset lines up with the first line of text next to
                // it, instead of sitting above it.
                LineHeight = _options.BaseFontSize * _options.LineHeightMultiplier,
                TextAlignment = Avalonia.Media.TextAlignment.Right,
                Margin = new Avalonia.Thickness(0, 0, 4, 0),
            };

        var content = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        foreach (var child in listItem)
        {
            var control = RenderBlock(child);
            if (control is not null)
            {
                content.Children.Add(control);
            }
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { marker, content },
        };
    }

    private static bool TryGetTaskListMarker(ListItemBlock listItem, out bool isChecked)
    {
        if (listItem.FirstOrDefault() is ParagraphBlock { Inline: { } inline } &&
            inline.FirstChild is TaskList taskList)
        {
            isChecked = taskList.Checked;
            return true;
        }

        isChecked = false;
        return false;
    }

    private Control RenderCodeBlock(CodeBlock code)
    {
        var text = new TextBlock
        {
            Text = code.Lines.ToString(),
            FontFamily = "monospace",
            FontSize = _options.BaseFontSize,
            TextWrapping = TextWrapping.NoWrap,
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(12, 8),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = text,
            },
        };
    }

    private Control RenderTable(Table table)
    {
        var grid = new Grid();
        var columnCount = table.ColumnDefinitions.Count;
        for (var c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        var rowIndex = 0;
        foreach (var rowBlock in table)
        {
            if (rowBlock is not TableRow row)
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var columnIndex = 0;
            foreach (var cellBlock in row)
            {
                if (cellBlock is not TableCell cell)
                {
                    continue;
                }

                var cellPanel = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var child in cell)
                {
                    var control = RenderBlock(child);
                    if (control is not null)
                    {
                        cellPanel.Children.Add(control);
                    }
                }

                var border = new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Avalonia.Thickness(0, 0, 1, 1),
                    Opacity = 1,
                    Padding = new Avalonia.Thickness(8, 4),
                    Child = cellPanel,
                    [Grid.RowProperty] = rowIndex,
                    [Grid.ColumnProperty] = columnIndex,
                };

                if (row.IsHeader)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(15, 128, 128, 128));
                }

                grid.Children.Add(border);
                columnIndex++;
            }

            rowIndex++;
        }

        return new Border { BorderBrush = Brushes.Gray, BorderThickness = new Avalonia.Thickness(1, 1, 0, 0), Child = grid };
    }

    private void AppendInlines(InlineCollection target, ContainerInline? container, List<(int Start, int End, string Url)> links, ref int position)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(target, inline, links, ref position);
        }
    }

    private void AppendInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline, List<(int Start, int End, string Url)> links, ref int position)
    {
        switch (inline)
        {
            case LiteralInline literal:
                {
                    var content = literal.Content.ToString();
                    target.Add(new Run(content));
                    position += content.Length;
                    break;
                }

            case LineBreakInline:
                target.Add(new LineBreak());
                position += Environment.NewLine.Length;
                break;

            case CodeInline code:
                target.Add(new Run(code.Content) { FontFamily = "monospace", Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)) });
                position += code.Content.Length;
                break;

            case EmphasisInline emphasis:
                {
                    var nested = new InlineCollection();
                    foreach (var child in emphasis)
                    {
                        AppendInline(nested, child, links, ref position);
                    }

                    Span span = emphasis.DelimiterCount == 2
                        ? new Bold()
                        : new Italic();
                    foreach (var n in nested)
                    {
                        span.Inlines.Add(n);
                    }

                    target.Add(span);
                    break;
                }

            case LinkInline { IsImage: true } image:
                {
                    // Reaching AppendInline at all means this image is genuinely mixed
                    // in with other inline content — a standalone "![alt](url)"
                    // paragraph (the common case) is intercepted earlier, see
                    // TryGetSoleImage, and rendered as an actual picture instead. This
                    // one is rendered as a plain clickable "[image: alt]" text link, not
                    // an embedded picture, because embedding a real Image control next
                    // to other text in the same TextBlock was verified (empirically) to
                    // silently blank the surrounding text — see decisions.md. Clicking
                    // it goes through the same link-activation path as a normal link
                    // (see AttachLinkHandling), which already knows how to open an image
                    // file externally.
                    var altText = ExtractPlainText(image);
                    var label = string.IsNullOrEmpty(altText) ? "[image]" : $"[image: {altText}]";
                    var start = position;
                    var span = new Span { Foreground = Brushes.DodgerBlue, TextDecorations = TextDecorations.Underline };
                    span.Inlines.Add(new Run(label));
                    position += label.Length;
                    target.Add(span);
                    links.Add((start, position, image.Url ?? string.Empty));
                    break;
                }

            case LinkInline link:
                {
                    // Rendered as an in-flow Span (not an embedded control) so it picks
                    // up the paragraph's FontSize/LineHeight/baseline for free — see
                    // AttachLinkHandling for how clicks and hover are handled instead.
                    var start = position;
                    var nested = new InlineCollection();
                    foreach (var child in link)
                    {
                        AppendInline(nested, child, links, ref position);
                    }

                    var span = new Span { Foreground = Brushes.DodgerBlue, TextDecorations = TextDecorations.Underline };
                    foreach (var n in nested)
                    {
                        span.Inlines.Add(n);
                    }

                    target.Add(span);
                    links.Add((start, position, link.Url ?? string.Empty));
                    break;
                }

            case TaskList:
                // Handled at the list-item level (see TryGetTaskListMarker) so it
                // doesn't also render as literal "[ ]" text here.
                break;

            case ContainerInline container:
                AppendInlines(target, container, links, ref position);
                break;

            default:
                if (inline is LeafInline leaf)
                {
                    var content = leaf.ToString() ?? string.Empty;
                    target.Add(new Run(content));
                    position += content.Length;
                }
                break;
        }
    }

    // Shared across every rendered document/image — a single small HttpClient is
    // meant to be reused for the process lifetime rather than one-per-request.
    // Timeout is deliberately short: RenderImage below blocks on this per remote
    // image (see its comment for why), so this bounds how long opening a document
    // can stall on one dead/slow URL.
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    // Only reached for a standalone "![alt](url)" paragraph — see TryGetSoleImage —
    // so this always renders a real, full-size picture. An image genuinely inline
    // with other text takes a different path entirely (see AppendInline's image
    // case) and never calls this.
    private Control RenderImage(LinkInline image)
    {
        var url = image.Url ?? string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is "http" or "https")
            {
                return RenderRemoteImage(uri, url);
            }

            // uri.IsFile also covers "file://host/..." UNC-style forms, not just
            // "file:///path" — LocalPath unescapes and strips the scheme either way.
            if (uri.IsFile)
            {
                return RenderLocalImage(uri.LocalPath, url);
            }
        }

        // Relative to the document's own directory (the common "./Build.png" case).
        // Path.IsPathRooted also lets an OS-absolute path (e.g. "/home/x/y.png" or
        // "C:\x\y.png") through unchanged instead of being (wrongly) combined with
        // _baseDirectory.
        var resolvedPath = Path.IsPathRooted(url) ? url : Path.Combine(_baseDirectory, url);
        return RenderLocalImage(resolvedPath, url);
    }

    private static Control RenderLocalImage(string resolvedPath, string originalUrl)
    {
        try
        {
            if (!File.Exists(resolvedPath))
            {
                return new TextBlock { Text = $"[missing image: {originalUrl}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
            }

            var bitmap = new Bitmap(resolvedPath);
            return new Image { Source = bitmap, MaxWidth = 800, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            return new TextBlock { Text = $"[unreadable image: {originalUrl}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
        }
    }

    // http(s) images are fetched synchronously (bounded by HttpClient's 5s timeout
    // above), blocking only the render of this one image — see decisions.md for why
    // this trades a bounded per-image stall for avoiding an async round-trip.
    // Task.Run + GetAwaiter().GetResult() (rather than a plain .Result on an
    // awaited-with-default-ConfigureAwait call) avoids deadlocking the UI thread: it
    // runs the fetch on a thread-pool thread with no captured Avalonia
    // SynchronizationContext, so its continuation never needs to marshal back onto
    // the (blocked) UI thread to complete.
    private static Control RenderRemoteImage(Uri uri, string originalUrl)
    {
        try
        {
            var bytes = Task.Run(() => HttpClient.GetByteArrayAsync(uri)).GetAwaiter().GetResult();
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            return new Image { Source = bitmap, MaxWidth = 800, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or NotSupportedException or ArgumentException)
        {
            return new TextBlock { Text = $"[unreachable image: {originalUrl}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
        }
    }

    // Flattens an image's alt-text inline content (which Markdig keeps as rich
    // inlines, e.g. "![*italic* alt](url)") down to plain text for the "[image:
    // alt]" fallback link — formatting within alt text isn't worth preserving here.
    private static string ExtractPlainText(ContainerInline container)
    {
        var text = new StringBuilder();
        AppendPlainText(container, text);
        return text.ToString();
    }

    private static void AppendPlainText(ContainerInline container, StringBuilder text)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case ContainerInline nested:
                    AppendPlainText(nested, text);
                    break;
            }
        }
    }
}
