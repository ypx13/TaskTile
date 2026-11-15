using System.Text.Json;

namespace TaskTile.Models;

/// <summary>Which screen edge app groups should pop up from.</summary>
public enum LaunchSide { Top = 1, Bottom = 2, Left = 3, Right = 4, Center = 5 }

/// <summary>Popup style: visual appearance of the popup.</summary>
public enum PopupStyle { Classic = 0, Compact = 1, Modern = 2, List = 3, Card = 4 }

/// <summary>Group type: app launcher vs file folder shortcut vs circular radial menu.</summary>
public enum GroupType { Apps = 0, Files = 1, Radial = 2 }

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
    public bool EnableAppEntranceAnimation { get; set; } = true;
    public bool AdaptiveRefreshRateAnimation { get; set; } = false;
    public double AnimationSpeedMultiplier { get; set; } = 1.0;
    public bool DisableAutoHide { get; set; } = false;
    public bool DisableFloat { get; set; } = false;
    public bool DisableRoundedCorners { get; set; } = false;
    public int TaskbarGap { get; set; } = 8; // Floating gap in pixels between taskbar edge and popup (default: 8px)
    public int RunningIndicatorStyle { get; set; } = 0; // 0 = Bar (Default), 1 = Corner Dot
    public bool HasAcknowledgedBarDetection { get; set; } = false;
    public string LastDetectedBar { get; set; } = string.Empty;

    // UI Styling & Custom Mix: Permanently cancelled per user request
    public bool YpxMixUI { get => false; set { } }

    // Secret Group Card Style (0: Classic, 1: Detailed, 2: Compact)
    public int GroupCardStyle { get; set; } = 0;

    // Experimental: Direct Taskbar Pinning Protocol (#pintotaskbarintent)
    public bool UsePinToTaskbarIntent { get; set; } = false;

    // Experimental: Rename App Groups to Niches
    public bool UseNichesTerminology { get; set; } = false;

    // Instant Pinning (pins immediately without consent notification prompt)
    public bool InstantTaskbarPin { get; set; } = false;

    // App Picker Options (Create Group Dialog)
    public bool ShowPickerIcons { get; set; } = true;
    public bool ShowPickerFilePaths { get; set; } = true;

    // Debug
    public bool PersistentDebugMode { get; set; } = false;

    // Windows Settings Personalization Card Integration (Locked behind feature flag for GitHub release)
    public bool EnableSettingsIntegration { get; set; } = false;

    // Backup & Restore subsystem enable toggle (outside backup page)
    public bool EnableBackupSystem { get; set; } = false;

    // Experimental: Show Admin Shield icon in top-right corner if app has Always Run as Administrator (on by default)
    public bool ShowAdminShieldBadge { get; set; } = true;

    // Administrator Shield Badge Color: 0 = Gold, 1 = Neutral
    public int AdminShieldColor { get; set; } = 0;

    // Live Notification Badges (WhatsApp, Discord, Mail, etc.)
    public bool ShowNotificationBadges { get; set; } = true;

    // Grayscale taskbar icon: show group icon in grayscale when popup is open
    public bool UseGrayscaleTaskbarIcon { get; set; } = false;

    // Changelog
    public string LastSeenVersion { get; set; } = "";
}
