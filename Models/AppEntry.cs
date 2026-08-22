namespace TaskTile.Models;

public class AppEntry
{
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    /// <summary>
    /// Path to the extracted .png icon cached on disk.
    /// May be empty if icon extraction failed.
    /// </summary>
    public string IconPath { get; set; } = string.Empty;
}
