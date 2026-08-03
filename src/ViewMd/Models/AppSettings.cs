namespace ViewMd.Models;

// Serialized as-is to ~/.config/view-md/settings.json.
public sealed class AppSettings
{
    public bool SidebarVisible { get; set; } = true;
    public double SidebarWidth { get; set; } = 260;
    public string Theme { get; set; } = "Default"; // Default | Light | Dark — Default follows the OS setting
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 700;

    // Appearance — see .charter/capabilities/preferences.md
    public string? FontFamily { get; set; } // null/empty = app default (bundled Inter)
    public double BaseFontSize { get; set; } = 14;
    public double LineHeightMultiplier { get; set; } = 1.1;
    public double DocumentMargin { get; set; } = 20;
}
