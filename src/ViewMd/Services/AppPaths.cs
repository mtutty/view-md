namespace ViewMd.Services;

public static class AppPaths
{
    // XDG Base Directory spec: $XDG_CONFIG_HOME, falling back to ~/.config.
    public static string ConfigDirectory { get; } = ResolveConfigDirectory();

    public static string MruFilePath => Path.Combine(ConfigDirectory, "mru.json");
    public static string SettingsFilePath => Path.Combine(ConfigDirectory, "settings.json");

    private static string ResolveConfigDirectory()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var baseDir = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        var dir = Path.Combine(baseDir, "view-md");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
