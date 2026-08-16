using System.Text.Json;

namespace TaskTile.Models;

/// <summary>Which screen edge app groups should pop up from.</summary>
public enum LaunchSide { Top = 1, Bottom = 2, Left = 3, Right = 4, Center = 5 }

/// <summary>Popup style: visual appearance of the popup.</summary>
public enum PopupStyle { Classic = 0, Compact = 1, Modern = 2, List = 3, Card = 4 }

/// <summary>Group type: app launcher vs file folder shortcut.</summary>
public enum GroupType { Apps = 0, Files = 1 }

public class AppSettings
{
    public int Theme { get; set; } = 0; // 0: System, 1: Light, 2: Dark
    public int BackdropStyle { get; set; } = 1; // 0: Acrylic, 1: Mica
    public int TitleAlignment { get; set; } = 1; // 0: Left, 1: Center, 2: Right
    public bool RunAtStartup { get; set; } = false;
    public bool EnableTrayIcon { get; set; } = true;
    public bool DisableWizard { get; set; } = true;
    public bool FirstRunComplete { get; set; } = false;
    public bool PinToStart { get; set; } = true;
    public bool SuppressExplorer { get; set; } = false;
    public bool AmoledMode { get; set; } = false;
    public bool ApplyGlobalConfigToPopups { get; set; } = true;
    public bool StartPopupsInBackground { get; set; } = true;

    // Bar detection / launch side
    public LaunchSide LaunchSide { get; set; } = LaunchSide.Bottom; // Defaulting to Bottom instead of Auto
    public bool GlobalMakeMainFocus { get; set; } = false;
    public bool DisableAnimation { get; set; } = false;
    public bool DisableAutoHide { get; set; } = false;
    public bool DisableFloat { get; set; } = false;
    public bool DisableRoundedCorners { get; set; } = false;
    public bool HasAcknowledgedBarDetection { get; set; } = false;
    public string LastDetectedBar { get; set; } = string.Empty;

    // Debug
    public bool PersistentDebugMode { get; set; } = false;
    // Changelog
    public string LastSeenVersion { get; set; } = "";
}
