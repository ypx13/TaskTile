                                                                                                                                            using System.Collections.ObjectModel;
using System.Text.Json;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Persists groups to %AppData%\TaskTile\groups.json
/// </summary>
public class GroupService
{
    private static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile");
    private static readonly string DataFile = Path.Combine(DataDir, "groups.json");

    private static GroupService? _instance;
    public static GroupService Instance => _instance ??= new GroupService();

    public ObservableCollection<AppGroup> Groups { get; private set; } = new();

    private GroupService()
    {
        Load();
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDir);
        var json = JsonSerializer.Serialize(Groups.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DataFile, json);
    }

    private void Load()
    {
        if (!File.Exists(DataFile)) return;
        try
        {
            var json = File.ReadAllText(DataFile);
            var list = JsonSerializer.Deserialize<List<AppGroup>>(json);
            if (list == null) return;

            // Fix legacy .lnk/.exe icon paths that cause 0xc0000005 crashes in WinUI BitmapImage
            foreach (var g in list)
            {
                foreach (var app in g.Apps)
                {
                    if (app.IconPath?.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) == true ||
                        app.IconPath?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        app.IconPath = "";
                    }
                }
            }

            Groups = new ObservableCollection<AppGroup>(list);
        }
        catch { /* corrupt file — start fresh */ }
    }

    public void AddGroup(AppGroup group)
    {
        Groups.Add(group);
        Save();
    }

    public void RemoveGroup(Guid id)
    {
        var g = Groups.FirstOrDefault(x => x.Id == id);
        if (g != null) Groups.Remove(g);
        Save();
    }

    public void UpdateGroup(AppGroup group)
    {
        var idx = Groups.IndexOf(Groups.First(x => x.Id == group.Id));
        if (idx >= 0) Groups[idx] = group;
        Save();
    }
}
