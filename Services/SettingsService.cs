using System;
using System.IO;
using System.Text.Json;
using TaskTile.Models;

namespace TaskTile.Services;

public static class SettingsService
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskTile", "settings.json");

    public static AppSettings Current { get; private set; } = new AppSettings();

    public static event Action? SettingsChanged;

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    Current = settings;
                }
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFile);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);

            SettingsChanged?.Invoke();
            
            if (Current.EnableTrayIcon) 
            {
                TaskTile.Helpers.SystemTrayManager.ShowSystemTray();
            }
            else
            {
                TaskTile.Helpers.SystemTrayManager.HideSystemTray();
            }
        }
        catch { }
    }
}
