using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Monitors Windows Notification Center (toasts) using UserNotificationListener
/// to display live notification badges (e.g. for WhatsApp, Mail, Discord, Brave, etc.) on app tiles.
/// </summary>
public static class NotificationBadgeService
{
    private static UserNotificationListener? _listener;
    private static bool _isInitialized = false;
    private static bool _isAccessGranted = false;
    private static readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, int> _mockCounts = new(StringComparer.OrdinalIgnoreCase);
    private static System.Threading.Timer? _pollTimer;

    /// <summary>
    /// DTM (Dev Test Mode): Inject a simulated notification count for any app name.
    /// Set count=0 to clear the mock. Fires NotificationCountsChanged.
    /// </summary>
    public static void SetMockCount(string appName, int count)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;
        if (count <= 0)
            _mockCounts.TryRemove(appName, out _);
        else
            _mockCounts[appName] = count;
        NotificationCountsChanged?.Invoke();
    }

    /// <summary>Returns mock count for an app name (DTM use only).</summary>
    public static int GetMockCount(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return 0;
        return _mockCounts.TryGetValue(appName, out int c) ? c : 0;
    }

    public static event Action? NotificationCountsChanged;

    public static void Initialize()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        Task.Run(async () =>
        {
            try
            {
                _listener = UserNotificationListener.Current;
                if (_listener == null) return;

                var status = await _listener.RequestAccessAsync();
                if (status == UserNotificationListenerAccessStatus.Allowed)
                {
                    _isAccessGranted = true;
                    _listener.NotificationChanged += Listener_NotificationChanged;
                    await RefreshCountsAsync();

                    // Periodic sync (every 8 seconds) in case notifications are dismissed outside events
                    _pollTimer = new System.Threading.Timer(async _ =>
                    {
                        try { await RefreshCountsAsync(); } catch { }
                    }, null, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(8));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationBadgeService] Init error: {ex.Message}");
            }
        });
    }

    private static async void Listener_NotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        try
        {
            await RefreshCountsAsync();
        }
        catch { }
    }

    public static async Task RefreshCountsAsync()
    {
        if (_listener == null || !_isAccessGranted) return;

        try
        {
            var notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            var newCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (notifs != null)
            {
                foreach (var n in notifs)
                {
                    string name = n.AppInfo?.DisplayInfo?.DisplayName?.Trim() ?? "";
                    string aumid = n.AppInfo?.AppUserModelId?.Trim() ?? "";

                    if (!string.IsNullOrEmpty(name))
                    {
                        newCounts[name] = newCounts.GetValueOrDefault(name) + 1;
                    }
                    if (!string.IsNullOrEmpty(aumid) && !aumid.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        newCounts[aumid] = newCounts.GetValueOrDefault(aumid) + 1;
                    }
                }
            }

            _counts.Clear();
            foreach (var kvp in newCounts)
            {
                _counts[kvp.Key] = kvp.Value;
            }

            NotificationCountsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationBadgeService] Refresh error: {ex.Message}");
        }
    }

    public static int GetNotificationCount(string appName, string exePath)
    {
        if (!SettingsService.Current.ShowNotificationBadges) return 0;

        string safeName = appName?.Trim() ?? "";
        string exeFile = "";
        try
        {
            if (!string.IsNullOrEmpty(exePath))
            {
                exeFile = Path.GetFileNameWithoutExtension(exePath).Trim();
            }
        }
        catch { }

        int count = 0;

        // DTM mock counts always checked first (works even without access)
        if (!_mockCounts.IsEmpty)
        {
            if (!string.IsNullOrEmpty(safeName) && _mockCounts.TryGetValue(safeName, out int m1))
                count = Math.Max(count, m1);
            if (!string.IsNullOrEmpty(exeFile) && _mockCounts.TryGetValue(exeFile, out int m2))
                count = Math.Max(count, m2);
        }

        // Real notification counts (only when access granted)
        if (_isAccessGranted && !_counts.IsEmpty)
        {
            // 1. Direct name match (e.g. "WhatsApp", "Brave")
            if (!string.IsNullOrEmpty(safeName) && _counts.TryGetValue(safeName, out int c1))
                count = Math.Max(count, c1);

            // 2. Direct exe name match (e.g. "WhatsApp", "brave")
            if (!string.IsNullOrEmpty(exeFile) && _counts.TryGetValue(exeFile, out int c2))
                count = Math.Max(count, c2);

            // 3. Substring / AUMID match (e.g. "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App" contains "WhatsApp")
            if (count == 0)
            {
                foreach (var (k, c) in _counts)
                {
                    if (!string.IsNullOrEmpty(safeName) && k.Contains(safeName, StringComparison.OrdinalIgnoreCase))
                        count = Math.Max(count, c);
                    else if (!string.IsNullOrEmpty(exeFile) && k.Contains(exeFile, StringComparison.OrdinalIgnoreCase))
                        count = Math.Max(count, c);
                }
            }
        }

        return count;
    }
}
