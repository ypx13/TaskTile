using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Creates launcher shortcuts on the user's Desktop.
/// Windows 11 completely blocks programmatic taskbar pinning via COM verbs,
/// so the most reliable approach is dropping the .lnk on the Desktop and selecting it in Explorer.
/// </summary>
public class TaskbarService
{
    private static readonly string DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private static readonly string LauncherExe = Path.Combine(
        AppContext.BaseDirectory, "TaskTile.exe");

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);
    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    public static event Action<AppGroup>? GroupPinChanged;

    private static readonly Guid CLSID_TaskbandPin = new Guid("90AA3A4E-1CBA-4233-B8BB-535773D48449");
    private static readonly Guid IID_IPinnedList3 = new Guid("0DD79AE2-D156-45D4-9EEB-3B549769E940");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        uint sfgaoIn,
        out uint psfgaoOut);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ModifyDelegate(IntPtr thisPtr, IntPtr pUnpin, IntPtr pPin, int caller);

    public static bool AttemptTaskbarPin(string lnkPath)
    {
        // ypx was TORTURED to insanity here ;_) reversing undocumented shell interfaces for direct taskbar pinning was pure pain
        // 1. Direct COM call via TaskbandPin (IPinnedList3 vtable slot 16) - identical to Edge & Firefox
        try
        {
            uint dummy = 0;
            IntPtr pidl = IntPtr.Zero;
            int hr = SHParseDisplayName(lnkPath, IntPtr.Zero, out pidl, 0, out dummy);
            if (hr == 0 && pidl != IntPtr.Zero)
            {
                try
                {
                    Guid clsid = CLSID_TaskbandPin;
                    Guid iid = IID_IPinnedList3;
                    IntPtr pPinnedList = IntPtr.Zero;
                    hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, ref iid, out pPinnedList);
                    if (hr == 0 && pPinnedList != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr vtbl = Marshal.ReadIntPtr(pPinnedList);
                            IntPtr modifySlot = Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size);
                            var modify = Marshal.GetDelegateForFunctionPointer<ModifyDelegate>(modifySlot);
                            int modHr = modify(pPinnedList, IntPtr.Zero, pidl, int.MaxValue);
                            if (modHr >= 0) return true;
                        }
                        finally
                        {
                            Marshal.Release(pPinnedList);
                        }
                    }
                }
                finally
                {
                    CoTaskMemFree(pidl);
                }
            }
        }
        catch { }

        // 2. Shell context verb attempt ("taskbarpin" / "Pin to taskbar")
        try
        {
            Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
            if (shellAppType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellAppType);
                if (shell != null)
                {
                    string dir = Path.GetDirectoryName(lnkPath)!;
                    string file = Path.GetFileName(lnkPath);
                    dynamic folder = shell.NameSpace(dir);
                    dynamic item = folder.ParseName(file);
                    dynamic verbs = item.Verbs();
                    for (int i = 0; i < verbs.Count; i++)
                    {
                        dynamic verb = verbs.Item(i);
                        string vName = verb.Name?.ToString() ?? "";
                        if (vName.Contains("taskbar", StringComparison.OrdinalIgnoreCase) ||
                            vName.Contains("pin", StringComparison.OrdinalIgnoreCase))
                        {
                            verb.DoIt();
                            return true;
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }

    public static bool AttemptTaskbarUnpin(string lnkPath)
    {
        try
        {
            uint dummy = 0;
            IntPtr pidl = IntPtr.Zero;
            int hr = SHParseDisplayName(lnkPath, IntPtr.Zero, out pidl, 0, out dummy);
            if (hr == 0 && pidl != IntPtr.Zero)
            {
                try
                {
                    Guid clsid = CLSID_TaskbandPin;
                    Guid iid = IID_IPinnedList3;
                    IntPtr pPinnedList = IntPtr.Zero;
                    hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out pPinnedList);
                    if (hr == 0 && pPinnedList != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr vtbl = Marshal.ReadIntPtr(pPinnedList);
                            IntPtr modifySlot = Marshal.ReadIntPtr(vtbl, 16 * IntPtr.Size);
                            var modify = Marshal.GetDelegateForFunctionPointer<ModifyDelegate>(modifySlot);
                            int modHr = modify(pPinnedList, pidl, IntPtr.Zero, int.MaxValue);
                            return modHr >= 0;
                        }
                        finally
                        {
                            Marshal.Release(pPinnedList);
                        }
                    }
                }
                finally
                {
                    CoTaskMemFree(pidl);
                }
            }
        }
        catch { }
        return false;
    }

    public static bool CheckIsPinningRestricted()
    {
        try
        {
            using var k1 = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer");
            if (k1?.GetValue("NoPinningToTaskbar") is int v1 && v1 == 1) return true;

            using var k2 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer");
            if (k2?.GetValue("NoPinningToTaskbar") is int v2 && v2 == 1) return true;
        }
        catch { }
        return false;
    }

    public static bool AllowPinningPolicy()
    {
        try
        {
            using var k1 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer");
            k1?.SetValue("NoPinningToTaskbar", 0, Microsoft.Win32.RegistryValueKind.DWord);
            k1?.DeleteValue("NoPinningToTaskbar", false);
            return true;
        }
        catch { }
        return false;
    }

    public static void PinGroup(AppGroup group)
    {
        bool isRestricted = CheckIsPinningRestricted();
        if (isRestricted)
        {
            App.MainWindowInstance?.ShowInAppNotification(
                "Taskbar Pinning Restricted",
                "Windows system policy is blocking taskbar pinning. Click 'Allow Pinning' to enable it.",
                "FIX_POLICY");
            return;
        }

        bool useTaskbandPin = SettingsService.Current.UsePinToTaskbarIntent;
        if (!useTaskbandPin)
        {
            // When TaskbandPin protocol is disabled, automatically create desktop shortcut and instruct user to drag to taskbar
            ExecutePin(group);
            return;
        }

        if (SettingsService.Current.InstantTaskbarPin)
        {
            ExecutePin(group);
            return;
        }

        // Browser-style consent notification: prompt the user to consent before pinning
        App.MainWindowInstance?.ShowInAppNotification(
            "Pin to Taskbar",
            $"Pin '{group.Name}' to your Windows taskbar?",
            "CONFIRM_PIN:" + group.Id);
    }

    public static void WarmupBridge()
    {
        System.Threading.Tasks.Task.Run(() => {
            try
            {
                TaskbarBridge.Ping();
            }
            catch { }
        });
    }

    #region Native Explorer Taskbar Bridge
    private static class TaskbarBridge
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        const uint PROCESS_ALL_ACCESS = 0x1FFFFF;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint MEM_RELEASE = 0x8000;
        const uint PAGE_READWRITE = 0x04;

        private static bool EnsureBridgeInjected()
        {
            try
            {
                string dllPath = Path.Combine(AppContext.BaseDirectory, "TaskbarPinBridge.dll");
                if (!File.Exists(dllPath))
                {
                    string devPath = Path.Combine(Directory.GetCurrentDirectory(), "TaskbarPinBridge.dll");
                    if (File.Exists(devPath)) dllPath = devPath;
                    else return false;
                }

                int explorerPid = 0;
                IntPtr hShellWnd = GetShellWindow();
                if (hShellWnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(hShellWnd, out uint shellPid);
                    explorerPid = (int)shellPid;
                }

                if (explorerPid == 0)
                {
                    var explorers = Process.GetProcessesByName("explorer");
                    if (explorers.Length == 0) return false;
                    explorerPid = explorers[0].Id;
                }

                IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, explorerPid);
                if (hProc == IntPtr.Zero) return false;

                try
                {
                    byte[] bytes = System.Text.Encoding.Unicode.GetBytes(dllPath + "\0");
                    IntPtr pRemoteMem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)bytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
                    if (pRemoteMem == IntPtr.Zero) return false;

                    try
                    {
                        if (!WriteProcessMemory(hProc, pRemoteMem, bytes, (uint)bytes.Length, out _))
                            return false;

                        IntPtr pLoadLibraryW = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
                        IntPtr hThread = CreateRemoteThread(hProc, IntPtr.Zero, 0, pLoadLibraryW, pRemoteMem, 0, IntPtr.Zero);
                        if (hThread == IntPtr.Zero) return false;

                        WaitForSingleObject(hThread, 3000);
                        CloseHandle(hThread);
                        return true;
                    }
                    finally
                    {
                        VirtualFreeEx(hProc, pRemoteMem, 0, MEM_RELEASE);
                    }
                }
                finally
                {
                    CloseHandle(hProc);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string SendCommand(string command, int timeoutMs = 2500)
        {
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", "TaskTilePinBridge", System.IO.Pipes.PipeDirection.InOut);
                client.Connect(timeoutMs);
                byte[] msgBytes = System.Text.Encoding.Unicode.GetBytes(command);
                client.Write(msgBytes, 0, msgBytes.Length);
                client.Flush();

                byte[] buf = new byte[1024];
                int read = client.Read(buf, 0, buf.Length);
                return System.Text.Encoding.Unicode.GetString(buf, 0, read);
            }
            catch
            {
                return "TIMEOUT";
            }
        }

        public static bool Ping()
        {
            string res = SendCommand("PING", 300);
            if (res == "PONG") return true;

            EnsureBridgeInjected();
            System.Threading.Thread.Sleep(200);

            res = SendCommand("PING", 1500);
            return res == "PONG";
        }

        public static bool Pin(string lnkPath)
        {
            string res = SendCommand("PIN|" + lnkPath, 400);
            if (res == "OK") return true;

            EnsureBridgeInjected();
            System.Threading.Thread.Sleep(300);

            res = SendCommand("PIN|" + lnkPath, 3500);
            return res == "OK";
        }

        public static bool Unpin(string lnkPath)
        {
            string res = SendCommand("UNPIN|" + lnkPath, 400);
            if (res == "OK") return true;

            EnsureBridgeInjected();
            System.Threading.Thread.Sleep(300);

            res = SendCommand("UNPIN|" + lnkPath, 3500);
            return res == "OK";
        }
    }
    #endregion

    private static void UpdateShortcutsInDirectory(string dir, AppGroup group, string? iconPath, string? fallbackFileName)
    {
        if (!Directory.Exists(dir)) return;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
            dynamic shell = Activator.CreateInstance(shellType)!;
            var lnkFiles = Directory.GetFiles(dir, "TaskTile - *.lnk");
            string targetArg = $"--group {group.Id}";
            bool foundAny = false;

            foreach (var f in lnkFiles)
            {
                try
                {
                    dynamic lnk = shell.CreateShortcut(f);
                    string? args = lnk.Arguments as string;
                    if (args != null && args.Contains(targetArg))
                    {
                        foundAny = true;
                        lnk.TargetPath = LauncherExe;
                        lnk.Description = group.Name;
                        lnk.WorkingDirectory = AppContext.BaseDirectory;
                        if (!string.IsNullOrEmpty(iconPath)) lnk.IconLocation = iconPath;
                        lnk.Save();
                        JumpListService.SetShortcutAppId(f, JumpListService.GetGroupAppId(group.Id));
                    }
                    Marshal.ReleaseComObject(lnk);
                }
                catch { }
            }

            Marshal.ReleaseComObject(shell);

            if (!foundAny && !string.IsNullOrEmpty(fallbackFileName))
            {
                string createdPath = Path.Combine(dir, fallbackFileName);
                CreateShortcut(createdPath, LauncherExe, targetArg, group.Name, AppContext.BaseDirectory, iconPath);
                JumpListService.SetShortcutAppId(createdPath, JumpListService.GetGroupAppId(group.Id));
            }
        }
        catch { }
    }

    public static void UpdateGroupShortcut(AppGroup group)
    {
        if (!group.IsPinned) return;
        System.Threading.Tasks.Task.Run(() => {
            try
            {
                string safeName = $"TaskTile - {group.Name}.lnk";
                string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "TaskTile");
                string localShortcutsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile", "Shortcuts");
                string userPinnedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

                string? iconPath = GenerateGroupIcon(group, out string? _);

                RunOnSta(() => {
                    UpdateShortcutsInDirectory(userPinnedDir, group, iconPath, null);
                    UpdateShortcutsInDirectory(startMenuDir, group, iconPath, safeName);
                    UpdateShortcutsInDirectory(DesktopDir, group, iconPath, safeName);
                    UpdateShortcutsInDirectory(localShortcutsDir, group, iconPath, safeName);
                });
                JumpListService.UpdateGroupJumpList(group);
            }
            catch { }
        });
    }

    public static void ExecutePin(AppGroup group)
    {
        System.Threading.Tasks.Task.Run(() => {
            string safeName = $"TaskTile - {group.Name}.lnk";
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "TaskTile");
            Directory.CreateDirectory(startMenuDir);
            string startMenuLnk = Path.Combine(startMenuDir, safeName);

            string desktopLnk = Path.Combine(DesktopDir, safeName);

            string localShortcutsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile", "Shortcuts");
            Directory.CreateDirectory(localShortcutsDir);
            string localLnk = Path.Combine(localShortcutsDir, safeName);

            string userPinnedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

            string? iconPath = GenerateGroupIcon(group, out string? _);

            bool alreadyPinned = false;
            RunOnSta(() => {
                // Update or check user pinned directory without ever deleting
                if (Directory.Exists(userPinnedDir))
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    var pinnedFiles = Directory.GetFiles(userPinnedDir, "TaskTile - *.lnk");
                    string targetArg = $"--group {group.Id}";
                    foreach (var f in pinnedFiles)
                    {
                        try
                        {
                            dynamic lnk = shell.CreateShortcut(f);
                            string? args = lnk.Arguments as string;
                            if (args != null && args.Contains(targetArg))
                            {
                                alreadyPinned = true;
                                lnk.TargetPath = LauncherExe;
                                lnk.Description = group.Name;
                                lnk.WorkingDirectory = AppContext.BaseDirectory;
                                if (!string.IsNullOrEmpty(iconPath)) lnk.IconLocation = iconPath;
                                lnk.Save();
                                JumpListService.SetShortcutAppId(f, JumpListService.GetGroupAppId(group.Id));
                            }
                            Marshal.ReleaseComObject(lnk);
                        }
                        catch { }
                    }
                    Marshal.ReleaseComObject(shell);
                }

                // Create or update shortcuts in Start Menu, Desktop, and Local AppData
                UpdateShortcutsInDirectory(startMenuDir, group, iconPath, safeName);
                UpdateShortcutsInDirectory(DesktopDir, group, iconPath, safeName);
                UpdateShortcutsInDirectory(localShortcutsDir, group, iconPath, safeName);
                JumpListService.UpdateGroupJumpList(group);
            });

            bool useTaskbandPin = SettingsService.Current.UsePinToTaskbarIntent;
            bool directSuccess = alreadyPinned;

            if (useTaskbandPin && !alreadyPinned)
            {
                // Automated Direct Taskbar Pinning via native Explorer bridge
                directSuccess = TaskbarBridge.Pin(startMenuLnk);
                if (!directSuccess)
                {
                    directSuccess = TaskbarBridge.Pin(desktopLnk);
                }
                if (!directSuccess)
                {
                    RunOnSta(() => {
                        directSuccess = AttemptTaskbarPin(startMenuLnk) || AttemptTaskbarPin(desktopLnk);
                    });
                }
            }

            group.IsPinned = true;
            GroupService.Instance.Save();
            GroupPinChanged?.Invoke(group);

            if (useTaskbandPin && directSuccess)
            {
                App.MainWindowInstance?.ShowInAppNotification(
                    "Pinned to Taskbar",
                    $"'{group.Name}' is now pinned directly to your taskbar!",
                    null);
            }
            else
            {
                App.MainWindowInstance?.ShowInAppNotification(
                    "Shortcut Created",
                    $"'{group.Name}' shortcut created on your desktop. Drag it to your taskbar to pin!",
                    desktopLnk);
            }
        });
    }

    public static void UnpinGroup(AppGroup group)
    {
        System.Threading.Tasks.Task.Run(() => {
            string safeName = $"TaskTile - {group.Name}.lnk";
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "TaskTile");
            string startMenuLnk = Path.Combine(startMenuDir, safeName);
            string desktopLnk = Path.Combine(DesktopDir, safeName);
            string localShortcutsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile", "Shortcuts");
            string localLnk = Path.Combine(localShortcutsDir, safeName);
            string userPinnedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");
            string userPinnedLnk = Path.Combine(userPinnedDir, safeName);

            // 1. Direct taskbar unpin via Explorer bridge for all matching shortcuts in User Pinned
            if (Directory.Exists(userPinnedDir))
            {
                try
                {
                    var pinnedFiles = Directory.GetFiles(userPinnedDir, $"TaskTile - {group.Name}*.lnk");
                    foreach (var pFile in pinnedFiles)
                    {
                        TaskbarBridge.Unpin(pFile);
                        try { File.Delete(pFile); } catch { }
                    }
                }
                catch { }
            }

            // 2. Also unpin Start Menu and Desktop shortcuts via bridge
            if (File.Exists(startMenuLnk)) TaskbarBridge.Unpin(startMenuLnk);
            if (File.Exists(desktopLnk)) TaskbarBridge.Unpin(desktopLnk);
            if (File.Exists(userPinnedLnk)) TaskbarBridge.Unpin(userPinnedLnk);

            RunOnSta(() => {
                if (File.Exists(startMenuLnk)) AttemptTaskbarUnpin(startMenuLnk);
                if (File.Exists(desktopLnk)) AttemptTaskbarUnpin(desktopLnk);
                if (File.Exists(localLnk)) AttemptTaskbarUnpin(localLnk);
                if (File.Exists(userPinnedLnk)) AttemptTaskbarUnpin(userPinnedLnk);
            });

            try { if (File.Exists(desktopLnk)) File.Delete(desktopLnk); } catch { }
            try { if (File.Exists(startMenuLnk)) File.Delete(startMenuLnk); } catch { }
            try { if (File.Exists(localLnk)) File.Delete(localLnk); } catch { }
            try { if (File.Exists(userPinnedLnk)) File.Delete(userPinnedLnk); } catch { }

            group.IsPinned = false;
            GroupService.Instance.Save();
            GroupPinChanged?.Invoke(group);

            App.MainWindowInstance?.ShowInAppNotification(
                "Unpinned",
                $"'{group.Name}' unpinned and removed.",
                null);
        });
    }

    private static void EnsureAumidRegistered()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\TaskTile");
            if (key != null)
            {
                key.SetValue("DisplayName", "TaskTile");
                string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "newicon.png");
                if (File.Exists(icon)) key.SetValue("IconUri", icon);
            }
        }
        catch { }
    }

    private static void SendPinIntentNotification(AppGroup group, string lnkPath, bool isDirectSuccess = false)
    {
        try
        {
            EnsureAumidRegistered();
            string safeName = System.Security.SecurityElement.Escape(group.Name);
            bool isNiche = SettingsService.Current.UseNichesTerminology;
            string titleTag = isDirectSuccess ? "TaskTile • Taskbar" : (SettingsService.Current.UsePinToTaskbarIntent ? "TaskTile • TaskbandPin" : "TaskTile");
            string prompt = isDirectSuccess ? $"'{safeName}' Pinned to Taskbar" : $"Pin '{safeName}' to Taskbar?";
            string msg = isDirectSuccess ? $"This {(isNiche ? "niche" : "group")} is now available on your taskbar." : $"Drag the desktop shortcut to your taskbar to pin '{safeName}'.";
            string xml = $@"
            <toast scenario=""reminder"">
                <visual>
                    <binding template=""ToastGeneric"">
                        <text>{titleTag}</text>
                        <text>{prompt}</text>
                        <text>{msg}</text>
                    </binding>
                </visual>
                <actions>
                    <action content=""Open Desktop"" arguments=""open_{group.Id}"" activationType=""foreground""/>
                </actions>
            </toast>";

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);
            var toast = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("TaskTile").Show(toast);
        }
        catch
        {
            try
            {
                string safeName = group.Name.Replace("'", "''").Replace("\"", "`\"");
                string psScript = $"[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null; $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02); $textNodes = $template.GetElementsByTagName('text'); $textNodes.Item(0).AppendChild($template.CreateTextNode('TaskTile • TaskbandPin')) | Out-Null; $textNodes.Item(1).AppendChild($template.CreateTextNode('Pin {safeName} to Taskbar?')) | Out-Null; $toast = [Windows.UI.Notifications.ToastNotification]::new($template); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('TaskTile').Show($toast);";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }
    }

    public static string? GenerateGroupIcon(AppGroup group, out string? pngPath)
    {
        pngPath = null;
        try
        {
            string iconsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile", "Icons");
            Directory.CreateDirectory(iconsDir);
            
            string baseFilename = $"Group_{group.Id}_{DateTime.Now.Ticks}";
            string iconPath = Path.Combine(iconsDir, $"{baseFilename}.ico");
            pngPath = Path.Combine(iconsDir, $"{baseFilename}.png");

            int size = 128; // High quality shortcut size
            using var bmp = new System.Drawing.Bitmap(size, size);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Handle custom user icon override
            if (!string.IsNullOrEmpty(group.CustomIconPath) && File.Exists(group.CustomIconPath))
            {
                try 
                {
                    using var customBmp = new System.Drawing.Bitmap(group.CustomIconPath);
                    g.DrawImage(customBmp, new System.Drawing.Rectangle(0, 0, size, size));
                }
                catch { /* fallback to default drawing if custom image is corrupted */ }
            }
            
            if (string.IsNullOrEmpty(group.CustomIconPath) || !File.Exists(group.CustomIconPath))
            {
                // Determine folder background based on Style Enum (0=Transparent (Default), 1=Normal (Dark), 2=Accent Style)
                if (group.FolderIconStyle == 2)
                {
                    var uiSettings = new Windows.UI.ViewManagement.UISettings();
                    var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                    System.Drawing.Color bgColor = System.Drawing.Color.FromArgb(255, accent.R, accent.G, accent.B);
                    using var bgBrush = new System.Drawing.SolidBrush(bgColor); 
                    int padding = 0;
                    int rectSize = size - padding * 2;
                    var rect = new System.Drawing.Rectangle(padding, padding, rectSize, rectSize);
                    
                    int cornerRadius = 24;
                    using var path = new System.Drawing.Drawing2D.GraphicsPath();
                    path.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
                    path.AddArc(rect.X + rect.Width - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
                    path.AddArc(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                    path.AddArc(rect.X, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                    path.CloseAllFigures();
                    
                    g.FillPath(bgBrush, path);
                }
                else if (group.FolderIconStyle == 1)
                {
                    // Normal (Dark) background
                    using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 36, 36, 36));
                    int padding = 0;
                    int rectSize = size - padding * 2;
                    var rect = new System.Drawing.Rectangle(padding, padding, rectSize, rectSize);
                    
                    int cornerRadius = 24;
                    using var path = new System.Drawing.Drawing2D.GraphicsPath();
                    path.AddArc(rect.X, rect.Y, cornerRadius, cornerRadius, 180, 90);
                    path.AddArc(rect.X + rect.Width - cornerRadius, rect.Y, cornerRadius, cornerRadius, 270, 90);
                    path.AddArc(rect.X + rect.Width - cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
                    path.AddArc(rect.X, rect.Y + rect.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
                    path.CloseAllFigures();
                    
                    g.FillPath(bgBrush, path);
                }

                // Collect up to 4 paths to draw
                var pathsToDraw = new List<string>();
                if (group.IsDynamicFolder && !string.IsNullOrEmpty(group.DynamicFolderPath) && Directory.Exists(group.DynamicFolderPath))
                {
                    var dirFiles = Directory.GetFileSystemEntries(group.DynamicFolderPath)
                        .Where(f => (new FileInfo(f).Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                        .Take(4).ToList();
                    pathsToDraw.AddRange(dirFiles);
                }
                else
                {
                    pathsToDraw.AddRange(group.Apps.Take(4).Select(a => a.ExePath));
                }
                
                // Adjust if transparent bg (0 = Transparent default)
                int innerPadding = group.FolderIconStyle == 0 ? 0 : 2;
                int innerRectSize = size - innerPadding * 2;
                
                int iconSize = 60;
                int spacing = 4;
                
                int totalIconSpan = (iconSize * 2) + spacing;
                int startX = innerPadding + (innerRectSize - totalIconSpan) / 2;
                int startY = innerPadding + (innerRectSize - totalIconSpan) / 2;

                if (pathsToDraw.Count <= 1)
                {
                    iconSize = 112;
                    startX = innerPadding + (innerRectSize - iconSize) / 2;
                    startY = innerPadding + (innerRectSize - iconSize) / 2;
                }

                if (pathsToDraw.Count == 0 && group.IsDynamicFolder)
                {
                    // Draw single folder icon
                    using var folderBmp = IconHelper.ExtractFolderIcon();
                    if (folderBmp != null)
                    {
                        g.DrawImage(folderBmp, new System.Drawing.Rectangle(startX, startY, iconSize, iconSize));
                    }
                }
                else
                {
                    for (int i = 0; i < pathsToDraw.Count; i++)
                    {
                        int row = 0;
                        int col = 0;
                        if (pathsToDraw.Count > 1)
                        {
                            row = i / 2;
                            col = i % 2;
                        }
                        
                        int x = startX + col * (iconSize + spacing);
                        int y = startY + row * (iconSize + spacing);
                        
                        try
                        {
                            string itemPath = pathsToDraw[i];
                            System.Drawing.Bitmap? iconBmp = null;

                            if (Directory.Exists(itemPath))
                            {
                                iconBmp = IconHelper.ExtractFolderIcon();
                            }
                            else if (File.Exists(itemPath))
                            {
                                string cached = IconHelper.GetOrExtractIcon(itemPath);
                                if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
                                {
                                    iconBmp = new System.Drawing.Bitmap(cached);
                                }
                                else
                                {
                                    using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(itemPath);
                                    if (sysIcon != null) iconBmp = sysIcon.ToBitmap();
                                }
                            }

                            if (iconBmp != null)
                            {
                                if (group.MonochromeFolderIcon)
                                {
                                    float[][] colorMatrixElements = { 
                                       new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                                       new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                                       new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                                       new float[] {0, 0, 0, 1, 0},
                                       new float[] {0, 0, 0, 0, 1}
                                    };
                                    var cm = new System.Drawing.Imaging.ColorMatrix(colorMatrixElements);
                                    using var ia = new System.Drawing.Imaging.ImageAttributes();
                                    ia.SetColorMatrix(cm);
                                    g.DrawImage(iconBmp, new System.Drawing.Rectangle(x, y, iconSize, iconSize), 0, 0, iconBmp.Width, iconBmp.Height, System.Drawing.GraphicsUnit.Pixel, ia);
                                }
                                else
                                {
                                    g.DrawImage(iconBmp, new System.Drawing.Rectangle(x, y, iconSize, iconSize));
                                }
                                iconBmp.Dispose();
                            }
                        }
                        catch { } // Ignore read errors for individual icons
                    }
                }
            }
            
            g.Flush();
            
            // Save PNG for UI preview
            bmp.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);

            // Save as raw 32-bit ICO format natively
            using var fs = new FileStream(iconPath, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            bw.Write((short)0);   // Reserved
            bw.Write((short)1);   // ICO type
            bw.Write((short)1);   // Count = 1
            
            bw.Write((byte)128);  // width
            bw.Write((byte)128);  // height
            bw.Write((byte)0);    // color count
            bw.Write((byte)0);    // reserved
            bw.Write((short)1);   // planes
            bw.Write((short)32);  // bpp

            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] pngBytes = ms.ToArray();
            
            bw.Write((int)pngBytes.Length); // size
            bw.Write((int)22);    // offset
            bw.Write(pngBytes);   // data

            return iconPath;
        }
        catch 
        {
            return null;
        }
    }

    // ─── Grayscale Icon Support ──────────────────────────────────────────────

    /// <summary>
    /// Generates a grayscale version of the group's taskbar icon and returns its .ico path.
    /// </summary>
    public static string? GenerateGrayscaleGroupIcon(AppGroup group)
    {
        try
        {
            string? sourcePng;
            string? sourceIco = GenerateGroupIcon(group, out sourcePng);
            if (sourceIco == null) return null;

            string iconsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TaskTile", "Icons");
            Directory.CreateDirectory(iconsDir);

            string grayIco = Path.Combine(iconsDir, $"Group_{group.Id}_grayscale.ico");

            string loadFrom = sourcePng != null && File.Exists(sourcePng) ? sourcePng : sourceIco;
            using var src = new System.Drawing.Bitmap(loadFrom);

            int size = src.Width;
            using var grayBmp = new System.Drawing.Bitmap(size, size);
            using var g = System.Drawing.Graphics.FromImage(grayBmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            float[][] colorMatrixElements = {
                new float[] {0.299f, 0.299f, 0.299f, 0, 0},
                new float[] {0.587f, 0.587f, 0.587f, 0, 0},
                new float[] {0.114f, 0.114f, 0.114f, 0, 0},
                new float[] {0,      0,      0,      1, 0},
                new float[] {0,      0,      0,      0, 1}
            };
            var cm = new System.Drawing.Imaging.ColorMatrix(colorMatrixElements);
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(cm);
            g.DrawImage(src, new System.Drawing.Rectangle(0, 0, size, size),
                        0, 0, src.Width, src.Height,
                        System.Drawing.GraphicsUnit.Pixel, ia);

            using var fs = new FileStream(grayIco, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)1);
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)0); bw.Write((byte)0);
            bw.Write((short)1); bw.Write((short)32);
            using var ms = new MemoryStream();
            grayBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] pngBytes = ms.ToArray();
            bw.Write((int)pngBytes.Length);
            bw.Write((int)22);
            bw.Write(pngBytes);

            return grayIco;
        }
        catch { return null; }
    }

    /// <summary>
    /// Applies a grayscale icon to the group's pinned taskbar shortcut (fire-and-forget).
    /// </summary>
    public static void ApplyGrayscaleTaskbarIcon(AppGroup group)
    {
        if (!group.IsPinned) return;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                string? grayIco = GenerateGrayscaleGroupIcon(group);
                if (grayIco == null) return;
                string userPinnedDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");
                RunOnSta(() => UpdateShortcutsInDirectory(userPinnedDir, group, grayIco, null));
                NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        });
    }

    /// <summary>
    /// Restores the normal colour icon on the group's pinned taskbar shortcut (fire-and-forget).
    /// </summary>
    public static void RestoreNormalTaskbarIcon(AppGroup group)
    {
        if (!group.IsPinned) return;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                string? iconPath = GenerateGroupIcon(group, out _);
                if (iconPath == null) return;
                string userPinnedDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");
                RunOnSta(() => UpdateShortcutsInDirectory(userPinnedDir, group, iconPath, null));
                NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var t = new Thread(() =>
        {
            uint oldMode = SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX);
            try { action(); }
            catch (Exception e) { ex = e; }
            finally { SetErrorMode(oldMode); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (ex != null) throw ex;
    }

    private static void CreateShortcut(string lnkPath, string targetPath,
        string arguments, string description, string workingDir, string? iconPath)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic lnk = shell.CreateShortcut(lnkPath);
        lnk.TargetPath = targetPath;
        lnk.Arguments = arguments;
        lnk.Description = description;
        lnk.WorkingDirectory = workingDir;
        
        if (!string.IsNullOrEmpty(iconPath))
        {
            lnk.IconLocation = iconPath;
        }
        else
        {
            lnk.IconLocation = @"%SystemRoot%\System32\imageres.dll, 3"; // Fallback folder icon
        }
        
        lnk.Save();
        Marshal.ReleaseComObject(lnk);
        Marshal.ReleaseComObject(shell);
    }
}
