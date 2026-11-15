using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Dispatching;
using TaskTile.Models;
using TaskTile.Popups;

namespace TaskTile.Services;

/// <summary>
/// Manages the lifecycle, multi-press triggers, and display of the Radial Menu (Pie Menu) window.
/// Supports:
/// - Single press: opens radial menu with a staggered train blossom animation.
/// - Double press: launches all apps in the group with a delayed train sequence.
/// - Triple press: launches edit mode for the radial menu.
/// </summary>
public static class RadialMenuManager
{
    private static RadialMenuWindow? _currentWindow;
    private static Guid? _lastTriggerGroupId;
    private static DateTime _lastTriggerTime = DateTime.MinValue;
    private static int _pressCount = 0;
    private static DispatcherQueueTimer? _multiPressTimer;

    public static RadialMenuWindow? CurrentWindow => _currentWindow;

    public static void Show(AppGroup group, bool isEditMode = false)
    {
        try
        {
            if (_currentWindow != null)
            {
                if (_currentWindow.GroupId == group.Id)
                {
                    if (isEditMode)
                    {
                        _currentWindow.EnterEditMode();
                        return;
                    }
                }

                try { _currentWindow.Close(); } catch { }
                _currentWindow = null;
            }

            _currentWindow = new RadialMenuWindow(group, isEditMode);
            _currentWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadialMenuManager] Error showing radial menu: {ex}");
        }
    }

    /// <summary>
    /// Handles hotkey or launcher triggers with multi-press detection:
    /// - 1 press: opens radial menu immediately
    /// - 2 presses within 400ms: launches all apps in delayed train
    /// - 3 presses within 400ms: launches edit mode
    /// </summary>
    public static void HandleTrigger(AppGroup group)
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastTriggerTime).TotalMilliseconds;

        if (_lastTriggerGroupId == group.Id && elapsed < 420)
        {
            _pressCount++;
        }
        else
        {
            _pressCount = 1;
            _lastTriggerGroupId = group.Id;
        }
        _lastTriggerTime = now;

        if (_pressCount == 1)
        {
            // First press: open immediately with delayed train bloom animation!
            Show(group, isEditMode: false);
        }
        else if (_pressCount == 2)
        {
            // Double press: wait up to 260ms for a possible 3rd press
            _multiPressTimer?.Stop();
            var dq = DispatcherQueue.GetForCurrentThread() ?? App.MainWindowInstance?.DispatcherQueue;
            if (dq != null)
            {
                _multiPressTimer = dq.CreateTimer();
                _multiPressTimer.Interval = TimeSpan.FromMilliseconds(260);
                _multiPressTimer.Tick += (s, e) =>
                {
                    _multiPressTimer?.Stop();
                    _multiPressTimer = null;
                    if (_pressCount == 2)
                    {
                        _pressCount = 0;
                        LaunchAllApps(group);
                    }
                };
                _multiPressTimer.Start();
            }
            else
            {
                LaunchAllApps(group);
            }
        }
        else if (_pressCount >= 3)
        {
            // Triple press: launch edit mode!
            _multiPressTimer?.Stop();
            _multiPressTimer = null;
            _pressCount = 0;

            if (_currentWindow != null && _currentWindow.GroupId == group.Id)
            {
                _currentWindow.EnterEditMode();
            }
            else
            {
                Show(group, isEditMode: true);
            }
        }
    }

    public static void LaunchAllApps(AppGroup group)
    {
        if (_currentWindow != null && _currentWindow.GroupId == group.Id)
        {
            _currentWindow.LaunchAllWithTrainAnim();
        }
        else
        {
            LaunchAppsInTrainSequence(group.Apps);
        }
    }

    public static void LaunchAppsInTrainSequence(IEnumerable<AppEntry> apps)
    {
        var appList = apps.ToList();
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            for (int i = 0; i < appList.Count; i++)
            {
                var app = appList[i];
                try
                {
                    if (!string.IsNullOrWhiteSpace(app.ExePath))
                    {
                        var psi = new ProcessStartInfo(app.ExePath)
                        {
                            UseShellExecute = true
                        };
                        try
                        {
                            var dir = Path.GetDirectoryName(app.ExePath);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                                psi.WorkingDirectory = dir;
                        }
                        catch { }
                        Process.Start(psi);
                    }
                }
                catch { }

                if (i < appList.Count - 1)
                {
                    await System.Threading.Tasks.Task.Delay(140); // Delayed train stagger between app launches
                }
            }
        });
    }

    public static void CloseCurrent()
    {
        if (_currentWindow != null)
        {
            try { _currentWindow.Close(); } catch { }
            _currentWindow = null;
        }
    }
}
