namespace ViewMd.Rendering;

// Decouples MarkdownRenderer from AppSettings' persistence format — MainWindow
// maps AppSettings -> this record when constructing a renderer.
public sealed record MarkdownRenderOptions(
    string? FontFamily,
    double BaseFontSize,
    double LineHeightMultiplier,
    double DocumentMargin)
{
    public static readonly MarkdownRenderOptions Default = new(
        FontFamily: null,
        BaseFontSize: 14,
        LineHeightMultiplier: 1.1,
        DocumentMargin: 20);
}
