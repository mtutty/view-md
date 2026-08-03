using System.Text.Json;
using ViewMd.Models;

namespace ViewMd.Services;

public sealed class SettingsService
{
    private readonly string _path;

    public AppSettings Current { get; }

    public SettingsService(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFilePath;
        Current = Load(_path);
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, AppJsonContext.Default.AppSettings);
            File.WriteAllText(_path, json);
        }
        catch (IOException)
        {
        }
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }

        return new AppSettings();
    }
}
