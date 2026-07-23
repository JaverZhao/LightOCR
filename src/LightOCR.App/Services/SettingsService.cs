using Serilog;
using System.IO;

namespace LightOCR.App.Services;

public class SettingsService
{
    private const int SchemaVersion = 1;

    public string SettingsPath { get; }

    public SettingsService(bool portableMode = false)
    {
        if (portableMode)
        {
            var appDir = AppContext.BaseDirectory;
            SettingsPath = Path.Combine(appDir, "data", "settings.json");
        }
        else
        {
            SettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LightOCR", "settings.json");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
    }

    public T Load<T>() where T : new()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var result = System.Text.Json.JsonSerializer.Deserialize<T>(json);
                if (result != null)
                    return result;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings, restoring defaults");
            BackupBrokenSettings();
        }

        return new T();
    }

    public void Save<T>(T settings)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);

        var tempPath = SettingsPath + ".tmp";
        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, SettingsPath, overwrite: true);
    }

    private void BackupBrokenSettings()
    {
        if (File.Exists(SettingsPath))
        {
            var backup = SettingsPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
            File.Move(SettingsPath, backup);
            Log.Information("Backed up broken settings to {Backup}", backup);
        }
    }
}
