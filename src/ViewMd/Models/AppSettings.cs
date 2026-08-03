namespace ViewMd.Models;

// Serialized as-is to ~/.config/view-md/settings.json.
public sealed class AppSettings
{
    public bool SidebarVisible { get; set; } = true;
    public double SidebarWidth { get; set; } = 260;
    public string Theme { get; set; } = "Default"; // Default | Light | Dark
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 700;
}
