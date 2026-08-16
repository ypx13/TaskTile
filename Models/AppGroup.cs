namespace TaskTile.Models;

public class AppGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<AppEntry> Apps { get; set; } = new();
    public bool IsPinned { get; set; } = false;
    public GroupType GroupType { get; set; } = GroupType.Apps; // Apps or Files
    public bool IsDynamicFolder { get; set; } = false;
    public string DynamicFolderPath { get; set; } = string.Empty;
    // Card Settings
    public bool HideName { get; set; } = false;
    public bool HideAppLabels { get; set; } = false;
    public bool ShowCardLabels { get; set; } = false;
    public int FolderIconStyle { get; set; } = 0; // 0 = Normal, 1 = Transparent, 2 = UWP (Accent)
    public bool MonochromeFolderIcon { get; set; } = false;
    public string CustomIconPath { get; set; } = string.Empty;
    
    // Popup Layout Settings
    public int PopupStyle { get; set; } = 0; // 0 = Classic, 1 = Compact (Row), 2 = Modern, 3 = List
    public int BackdropStyle { get; set; } = 0; // 0 = Acrylic, 1 = Mica, 2 = Transparent
    public int CompactAlignment { get; set; } = 0; // 0 = Top, 1 = Bottom
    public int TitleAlignment { get; set; } = -1; // -1 = Global, 0 = Left, 1 = Center, 2 = Right
    public bool LaunchAtCenter { get; set; } = false; // Kept for backwards compatibility if needed
    public bool MakeMainFocus { get; set; } = false;
    public bool IsDesktopMode { get; set; } = false;
    public bool OverrideLaunchSide { get; set; } = false;
    public bool OverrideBorderColor { get; set; } = false;
    public string CustomBorderColor { get; set; } = "#777777";
    public bool DisableAnimation { get; set; } = false;
    public bool DisableAutoHide { get; set; } = false;
    public bool DisableFloat { get; set; } = false;
    public bool DisableRoundedCorners { get; set; } = false;
    public bool KeepOpen { get; set; } = false;
    public int GroupLaunchSide { get; set; } = 0; // 0=Top, 1=Bottom, 2=Left, 3=Right, 4=Center

    // Advanced Rendering Settings
    public int AppIconStyle { get; set; } = 0; // 0 = Transparent/Normal, 1 = Monochrome, 2 = UWP Accent
    public int ThemeOverride { get; set; } = 0; // 0 = Global, 1 = Light, 2 = Dark
    public int GridColumns { get; set; } = 3;
    public int GridRows { get; set; } = 0; // 0 = Auto
}
