using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskTile.Services;

public static class AppRunningService
{
    private static readonly ConcurrentDictionary<string, List<int>> _launchedPidsByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> _shortcutCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When true, all apps report as currently running (DTM mode testing).
    /// </summary>
    public static bool FakeAppsRunningEnabled { get; set; } = false;

    // Win32 Interop
    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int WS_CHILD = 0x40000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>
    /// Checks if the Shift key is currently being held down.
    /// </summary>
    public static bool IsShiftPressed()
    {
        return (GetKeyState(0x10) & 0x8000) != 0 || (GetAsyncKeyState(0x10) & 0x8000) != 0;
    }

    /// <summary>
    /// Registers a newly launched process for an app path.
    /// </summary>
    public static void RegisterLaunchedProcess(string path, int pid)
    {
        if (string.IsNullOrWhiteSpace(path) || pid <= 0) return;

        AddTrackedPid(path, pid);

        string resolved = ResolveTargetPath(path);
        if (!string.Equals(resolved, path, StringComparison.OrdinalIgnoreCase))
        {
            AddTrackedPid(resolved, pid);
        }
    }

    private static void AddTrackedPid(string key, int pid)
    {
        var list = _launchedPidsByPath.GetOrAdd(key, _ => new List<int>());
        lock (list)
        {
            if (!list.Contains(pid)) list.Add(pid);
        }
    }

    /// <summary>
    /// Resolves shortcut (.lnk) targets to the real executable path.
    /// </summary>
    public static string ResolveTargetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;

        if (_shortcutCache.TryGetValue(path, out var cached)) return cached;

        string target = path;
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(path);
                string t = (string)shortcut.TargetPath;
                if (!string.IsNullOrWhiteSpace(t))
                {
                    target = t;
                }
            }
        }
        catch { }

        _shortcutCache[path] = target;
        return target;
    }

    /// <summary>
    /// Determines whether the app is currently running.
    /// </summary>
    public static bool IsAppRunning(string targetPath)
    {
        if (FakeAppsRunningEnabled) return true;
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        // 1. Check tracked PIDs for this path or its resolved target
        if (HasActiveTrackedPid(targetPath)) return true;

        string resolved = ResolveTargetPath(targetPath);
        if (!string.Equals(resolved, targetPath, StringComparison.OrdinalIgnoreCase) && HasActiveTrackedPid(resolved))
        {
            return true;
        }

        // 2. Check by Process Name
        var candidateNames = GetCandidateProcessNames(resolved, targetPath);
        foreach (var procName in candidateNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(procName);
                if (procs.Length > 0)
                {
                    // Check if at least one process has a visible window, or is alive
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (HasTopLevelWindow(p.Id, p.MainWindowHandle))
                            {
                                return true;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return false;
    }

    private static bool HasActiveTrackedPid(string key)
    {
        if (_launchedPidsByPath.TryGetValue(key, out var pids))
        {
            lock (pids)
            {
                for (int i = pids.Count - 1; i >= 0; i--)
                {
                    int pid = pids[i];
                    try
                    {
                        var p = Process.GetProcessById(pid);
                        if (!p.HasExited)
                        {
                            return true;
                        }
                        pids.RemoveAt(i);
                    }
                    catch
                    {
                        pids.RemoveAt(i);
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Gets candidate process names for a target path or UWP app.
    /// </summary>
    private static List<string> GetCandidateProcessNames(string resolved, string original)
    {
        var names = new List<string>();

        string name = Path.GetFileNameWithoutExtension(resolved);
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name);
        }

        string origName = Path.GetFileNameWithoutExtension(original);
        if (!string.IsNullOrWhiteSpace(origName) && !names.Contains(origName, StringComparer.OrdinalIgnoreCase))
        {
            names.Add(origName);
        }

        // UWP / Known App Aliases
        if (names.Any(n => n.Equals("calc", StringComparison.OrdinalIgnoreCase) || n.Contains("Calculator", StringComparison.OrdinalIgnoreCase)))
        {
            names.Add("CalculatorApp");
            names.Add("Calculator");
        }
        if (names.Any(n => n.Contains("Terminal", StringComparison.OrdinalIgnoreCase)))
        {
            names.Add("WindowsTerminal");
        }
        if (names.Any(n => n.Contains("Notepad", StringComparison.OrdinalIgnoreCase)))
        {
            names.Add("Notepad");
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Tries to bring the currently running instance of the app to the foreground.
    /// </summary>
    public static bool TryActivateRunningInstance(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        // 1. Try tracked PIDs first
        var pids = GetTrackedPids(targetPath);
        string resolved = ResolveTargetPath(targetPath);
        if (!string.Equals(resolved, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            pids.AddRange(GetTrackedPids(resolved));
        }

        foreach (var pid in pids)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (!p.HasExited)
                {
                    IntPtr hWnd = FindBestWindowForProcess(p.Id, p.MainWindowHandle);
                    if (hWnd != IntPtr.Zero && ActivateWindow(hWnd))
                    {
                        return true;
                    }
                }
            }
            catch { }
        }

        // 2. Try by process names
        var candidateNames = GetCandidateProcessNames(resolved, targetPath);
        foreach (var procName in candidateNames)
        {
            try
            {
                var procs = Process.GetProcessesByName(procName);
                foreach (var p in procs)
                {
                    try
                    {
                        IntPtr hWnd = FindBestWindowForProcess(p.Id, p.MainWindowHandle);
                        if (hWnd != IntPtr.Zero && ActivateWindow(hWnd))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        return false;
    }

    private static List<int> GetTrackedPids(string key)
    {
        if (_launchedPidsByPath.TryGetValue(key, out var list))
        {
            lock (list)
            {
                return list.ToList();
            }
        }
        return new List<int>();
    }

    public static bool HasTopLevelWindow(int pid, IntPtr mainHandle)
    {
        if (mainHandle != IntPtr.Zero && IsWindow(mainHandle) && (IsWindowVisible(mainHandle) || IsIconic(mainHandle)))
        {
            return true;
        }

        bool found = false;
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint wPid);
            if (wPid == pid && (IsWindowVisible(hWnd) || IsIconic(hWnd)))
            {
                int style = GetWindowLong(hWnd, GWL_STYLE);
                if ((style & WS_CHILD) == 0)
                {
                    found = true;
                    return false; // stop enumeration
                }
            }
            return true;
        }, IntPtr.Zero);

        return found;
    }

    public static IntPtr FindBestWindowForProcess(int pid, IntPtr mainHandle)
    {
        if (mainHandle != IntPtr.Zero && IsWindow(mainHandle) && (IsWindowVisible(mainHandle) || IsIconic(mainHandle)))
        {
            return mainHandle;
        }

        IntPtr best = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            GetWindowThreadProcessId(hWnd, out uint wPid);
            if (wPid == pid && (IsWindowVisible(hWnd) || IsIconic(hWnd)))
            {
                int style = GetWindowLong(hWnd, GWL_STYLE);
                int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                if ((style & WS_CHILD) == 0)
                {
                    GetWindowRect(hWnd, out RECT rect);
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    var sb = new StringBuilder(256);
                    GetWindowText(hWnd, sb, 256);

                    // Prefer standard window with size and title over hidden toolwindows
                    if ((exStyle & WS_EX_TOOLWINDOW) == 0 && w > 80 && h > 80)
                    {
                        best = hWnd;
                        return false;
                    }
                    if (best == IntPtr.Zero && (w > 30 || sb.Length > 0))
                    {
                        best = hWnd;
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        return best;
    }

    public static bool ActivateWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;

        if (IsIconic(hWnd))
        {
            ShowWindowAsync(hWnd, SW_RESTORE);
        }
        else
        {
            ShowWindowAsync(hWnd, SW_SHOW);
        }

        IntPtr fg = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fg, out _);
        uint curThread = GetCurrentThreadId();

        if (fgThread != curThread && fgThread != 0)
        {
            AttachThreadInput(curThread, fgThread, true);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
            AttachThreadInput(curThread, fgThread, false);
        }
        else
        {
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }

        return true;
    }
}
