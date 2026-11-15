using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TaskTile.Services;

/// <summary>
/// Monitors for Windows Settings (SystemSettings.exe) and automatically injects
/// SettingsCardBridge.dll to insert the native TaskTile card into the Personalization page.
/// </summary>
public static class SettingsCardService
{
    private static readonly HashSet<int> _injectedPids = new();
    private static CancellationTokenSource? _cts;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtResumeProcess(IntPtr hProcess);

    private const uint PROCESS_ALL_ACCESS = 0x1FFFFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint PAGE_READWRITE = 0x04;

    private static void Log(string msg)
    {
        System.Diagnostics.Debug.WriteLine($"[SettingsCardService] {msg}");
    }

    public static void StartMonitoring()
    {
        if (_cts != null) return;

        // Feature flag: locked behind flag for GitHub release, on by default for local environment
        bool isEnabled = SettingsService.Current.EnableSettingsIntegration;
        string localFlag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile", "enable_settings_card.flag");
        if (File.Exists(localFlag) || File.Exists(Path.Combine(AppContext.BaseDirectory, "enable_settings_card.flag")))
        {
            isEnabled = true;
        }
#if DEBUG
        isEnabled = true;
#endif

        if (!isEnabled)
        {
            Log("Settings integration is disabled by feature flag.");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Log("StartMonitoring called (Settings integration enabled)");
        EnsureStartupRegistered();

        Task.Run(async () =>
        {
            Log("Monitoring loop started");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    CheckAndInject();
                }
                catch (Exception ex)
                {
                    Log($"CheckAndInject exception: {ex}");
                }

                try
                {
                    await Task.Delay(120, token); // Fast 120ms polling for instant card injection
                }
                catch { break; }
            }
            Log("Monitoring loop ended");
        }, token);
    }

    public static void EnsureStartupRegistered()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath) && !exePath.Contains("dotnet"))
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("TaskTile", $"\"{exePath}\" --background");
                Log($"Registered startup: {exePath}");
            }
        }
        catch (Exception ex)
        {
            Log($"EnsureStartupRegistered error: {ex}");
        }
    }

    public static void StopMonitoring()
    {
        _cts?.Cancel();
        _cts = null;
    }

    public static void CheckAndInject()
    {
        string dllPath = Path.Combine(AppContext.BaseDirectory, "SettingsCardBridge.dll");
        if (!File.Exists(dllPath))
        {
            string devPath = Path.Combine(Directory.GetCurrentDirectory(), "SettingsCardBridge.dll");
            if (File.Exists(devPath)) dllPath = devPath;
            else
            {
                Log($"DLL not found in {dllPath} or {devPath}");
                return;
            }
        }

        var processes = Process.GetProcessesByName("SystemSettings");
        var activePids = new HashSet<int>();

        foreach (var proc in processes)
        {
            int pid = proc.Id;
            activePids.Add(pid);

            lock (_injectedPids)
            {
                if (_injectedPids.Contains(pid)) continue;
            }

            Log($"Attempting injection into PID {pid} using {dllPath}");
            if (InjectDll(pid, dllPath))
            {
                Log($"Successfully injected into PID {pid}");
                lock (_injectedPids)
                {
                    _injectedPids.Add(pid);
                }
            }
            else
            {
                Log($"Failed injection into PID {pid}");
            }
        }

        lock (_injectedPids)
        {
            _injectedPids.RemoveWhere(p => !activePids.Contains(p));
        }
    }

    private static bool InjectDll(int pid, string dllPath)
    {
        IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc == IntPtr.Zero)
        {
            Log($"OpenProcess failed for PID {pid}, error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        // Unsuspend UWP process if Windows suspended it in background
        try { NtResumeProcess(hProc); } catch { }

        try
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
            IntPtr pRemoteMem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (pRemoteMem == IntPtr.Zero)
            {
                Log($"VirtualAllocEx failed for PID {pid}, error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            try
            {
                if (!WriteProcessMemory(hProc, pRemoteMem, bytes, (uint)bytes.Length, out _))
                {
                    Log($"WriteProcessMemory failed for PID {pid}, error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                IntPtr pKernel32 = GetModuleHandle("kernel32.dll");
                IntPtr pLoadLibraryW = GetProcAddress(pKernel32, "LoadLibraryW");
                IntPtr hThread = CreateRemoteThread(hProc, IntPtr.Zero, 0, pLoadLibraryW, pRemoteMem, 0, IntPtr.Zero);
                if (hThread == IntPtr.Zero)
                {
                    Log($"CreateRemoteThread failed for PID {pid}, error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                uint waitRes = WaitForSingleObject(hThread, 5000);
                Log($"WaitForSingleObject result: {waitRes} for PID {pid}");
                CloseHandle(hThread);
                return true;
            }
            finally
            {
                // We keep memory reserved for module path during loading
            }
        }
        catch (Exception ex)
        {
            Log($"InjectDll exception for PID {pid}: {ex}");
            return false;
        }
        finally
        {
            CloseHandle(hProc);
        }
    }
}
