using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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

    public MarkdownRenderer(string baseDirectory, Action<string> onLinkActivated, MarkdownRenderOptions options)
    {
        _baseDirectory = baseDirectory;
        _onLinkActivated = onLinkActivated;
        _options = options;
    }

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
                return RenderParagraph(paragraph);
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
        AppendInlines(text.Inlines!, heading.Inline);
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
        AppendInlines(text.Inlines!, paragraph.Inline);
        return text;
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

    private void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(target, inline);
        }
    }

    private void AppendInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                break;

            case LineBreakInline:
                target.Add(new LineBreak());
                break;

            case CodeInline code:
                target.Add(new Run(code.Content) { FontFamily = "monospace", Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)) });
                break;

            case EmphasisInline emphasis:
                {
                    var nested = new InlineCollection();
                    foreach (var child in emphasis)
                    {
                        AppendInline(nested, child);
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
                target.Add(new InlineUIContainer { Child = RenderImage(image) });
                break;

            case LinkInline link:
                {
                    var linkText = new TextBlock { Foreground = Brushes.DodgerBlue, TextDecorations = TextDecorations.Underline };
                    var linkInlines = linkText.Inlines ??= [];
                    foreach (var child in link)
                    {
                        AppendInline(linkInlines, child);
                    }

                    var url = link.Url ?? string.Empty;
                    linkText.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                    linkText.PointerPressed += (_, _) => _onLinkActivated(url);
                    target.Add(new InlineUIContainer { Child = linkText });
                    break;
                }

            case TaskList:
                // Handled at the list-item level (see TryGetTaskListMarker) so it
                // doesn't also render as literal "[ ]" text here.
                break;

            case ContainerInline container:
                AppendInlines(target, container);
                break;

            default:
                if (inline is LeafInline leaf)
                {
                    target.Add(new Run(leaf.ToString() ?? string.Empty));
                }
                break;
        }
    }

    private Control RenderImage(LinkInline image)
    {
        var url = image.Url ?? string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            // No network fetch in v1 — keep startup/render latency independent of
            // network state. Shown as a plain label instead of a broken image icon.
            return new TextBlock { Text = $"[image: {url}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
        }

        try
        {
            var resolvedPath = Path.IsPathRooted(url) ? url : Path.Combine(_baseDirectory, url);
            if (!File.Exists(resolvedPath))
            {
                return new TextBlock { Text = $"[missing image: {url}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
            }

            var bitmap = new Bitmap(resolvedPath);
            return new Image { Source = bitmap, MaxWidth = 800, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException)
        {
            return new TextBlock { Text = $"[unreadable image: {url}]", Opacity = 0.6, FontStyle = FontStyle.Italic };
        }
    }
}
