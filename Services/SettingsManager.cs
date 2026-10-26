using System.IO;
using System.Text.Json;

namespace MeaningOverlay.Services;

public record AppSettings(string ApiKey = "<API kEY>", bool DetailedMode = false, string PrimaryModel = "pollinations");

/// <summary>
/// Persists settings to %AppData%\MeaningOverlay\settings.json
/// </summary>
public static class SettingsManager
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeaningOverlay");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public static void EnsureSettingsFile()
    {
        Directory.CreateDirectory(SettingsDir);
        if (!File.Exists(SettingsPath))
            SaveSettings(new AppSettings());
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, _opts));
    }
}
