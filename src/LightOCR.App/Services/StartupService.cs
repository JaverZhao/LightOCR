using Microsoft.Win32;
using Serilog;

namespace LightOCR.App.Services;

public static class StartupService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "LightOCR";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        var value = key?.GetValue(AppName) as string;
        return !string.IsNullOrEmpty(value);
    }

    public static void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;
            var exePath = $"\"{Environment.ProcessPath}\" --background";
            key.SetValue(AppName, exePath, RegistryValueKind.String);
            Log.Information("Startup enabled: {Path}", exePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to enable startup");
        }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key?.GetValue(AppName) != null)
                key.DeleteValue(AppName);
            Log.Information("Startup disabled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to disable startup");
        }
    }
}
