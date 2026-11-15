using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Manages Windows Taskbar Jump Lists and AppUserModelID registrations for App Groups.
/// Enables native right-click jump lists ("Edit app group", "Edit app group (Parent)", "Rename app group")
/// on pinned taskbar icons and popup windows.
/// </summary>
public static class JumpListService
{
    private static readonly Guid CLSID_DestinationList = new("77f10cf0-3db5-4966-b520-b7c54fd35ed6");
    private static readonly Guid CLSID_EnumerableObjectCollection = new("2d3468c1-36a7-43b6-ac24-d3f02fd9607a");
    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IObjectArray = new("92CA9DCD-5622-4bba-A805-5E9F541BD8C9");
    private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");

    private static readonly PROPERTYKEY PKEY_Title = new(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
        public PROPERTYKEY(Guid g, uint p) { fmtid = g; pid = p; }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;

        public static PROPVARIANT FromString(string val)
        {
            var pv = new PROPVARIANT();
            pv.vt = 31; // VT_LPWSTR
            pv.pwszVal = Marshal.StringToCoTaskMemUni(val);
            return pv;
        }

        public void Clear()
        {
            if (vt == 31 && pwszVal != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pwszVal);
                pwszVal = IntPtr.Zero;
                vt = 0;
            }
        }
    }

    [ComImport]
    [Guid("6332debf-87b5-4670-90c0-5e57b408a49e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        [PreserveSig]
        int BeginList(out uint pcMinSlots, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
        [PreserveSig]
        int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, [In, MarshalAs(UnmanagedType.Interface)] object poa);
        void AppendKnownCategory(int category);
        [PreserveSig]
        int AddUserTasks([In, MarshalAs(UnmanagedType.Interface)] object poa);
        void CommitList();
        void GetRemovedDestinations([In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void AbortList();
    }

    [ComImport]
    [Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IObjectArray
    {
        void GetCount(out uint pcObjects);
        void GetAt(uint uiIndex, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
    }

    [ComImport]
    [Guid("5632b1a4-e38a-400a-928a-d4cd63230295")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IObjectCollection : IObjectArray
    {
        new void GetCount(out uint pcObjects);
        new void GetAt(uint uiIndex, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);
        void AddObject([MarshalAs(UnmanagedType.Interface)] object punk);
        void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid, out IPropertyStore ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

    public static string GetGroupAppId(Guid groupId) => $"TaskTile.AppGroup.{groupId:D}";
    public static string GetGroupAppId(string groupId) => $"TaskTile.AppGroup.{groupId}";

    public static string? CurrentProcessAppId { get; private set; }

    public static string HostExePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "TaskTile.exe");

    public static void SetCurrentProcessAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return;
        try
        {
            int hr = SetCurrentProcessExplicitAppUserModelID(appId);
            if (hr >= 0)
                CurrentProcessAppId = appId;
            else
                System.Diagnostics.Debug.WriteLine($"[JumpListService] SetCurrentProcessExplicitAppUserModelID('{appId}') hr=0x{hr:X8}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JumpListService] SetCurrentProcessAppId error: {ex.Message}");
        }
    }

    public sealed class LaunchArgs
    {
        public Guid GroupId { get; set; }
        public string? Action { get; set; }
        public bool UpdateJumpList { get; set; }
        public string? RunPath { get; set; }
        public string? RunArgs { get; set; }
        public bool RunAdmin { get; set; }
    }

    public static LaunchArgs ParseArgs(string[] args)
    {
        var result = new LaunchArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--group" && i + 1 < args.Length)
            {
                Guid.TryParse(args[++i], out var gid);
                result.GroupId = gid;
            }
            else if (a == "--edit") result.Action = "edit";
            else if (a == "--parent-settings" || a == "--settings") result.Action = "parent-settings";
            else if (a == "--rename") result.Action = "rename";
            else if (a == "--update-jumplist") result.UpdateJumpList = true;
            else if (a == "--run" && i + 1 < args.Length) result.RunPath = args[++i];
            else if (a == "--run-args" && i + 1 < args.Length) result.RunArgs = args[++i];
            else if (a == "--run-admin") result.RunAdmin = true;
        }
        return result;
    }

    public static void LaunchHostedApp(Guid groupId, string runPath, string? runArgs, bool runAdmin)
    {
        try
        {
            SettingsService.Load();
            AppEntry? app = null;
            if (groupId != Guid.Empty)
            {
                var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == groupId);
                app = group?.Apps.FirstOrDefault(a =>
                    string.Equals(a.ExePath, runPath, StringComparison.OrdinalIgnoreCase));
            }

            var psi = new ProcessStartInfo(runPath)
            {
                UseShellExecute = true
            };
            string? arguments = runArgs ?? app?.Arguments;
            if (!string.IsNullOrWhiteSpace(arguments))
                psi.Arguments = arguments;
            if (runAdmin || (app?.RunAsAdmin ?? false))
                psi.Verb = "runas";
            try
            {
                if (File.Exists(runPath) || Directory.Exists(runPath))
                {
                    string? dir = Path.GetDirectoryName(runPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        psi.WorkingDirectory = dir;
                }
            }
            catch { }
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JumpListService] LaunchHostedApp error: {ex.Message}");
        }
    }

    private static void RegisterGroupAumid(string appId, string displayName, string? iconPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\" + appId);
            if (key == null) return;
            key.SetValue("DisplayName", string.IsNullOrWhiteSpace(displayName) ? "TaskTile" : displayName);
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                key.SetValue("IconUri", iconPath);
        }
        catch { }
    }

    public static void SetWindowAppId(IntPtr hwnd, string appId)
    {
        if (hwnd == IntPtr.Zero || string.IsNullOrEmpty(appId)) return;
        try
        {
            Guid iid = IID_IPropertyStore;
            int hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
            if (hr == 0 && store != null)
            {
                var pkey = PKEY_AppUserModel_ID;
                var pv = PROPVARIANT.FromString(appId);
                store.SetValue(ref pkey, ref pv);
                pv.Clear();
                store.Commit();
                Marshal.ReleaseComObject(store);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JumpListService] SetWindowAppId error: {ex.Message}");
        }
    }

    public static bool SetShortcutAppId(string lnkPath, string appId)
    {
        if (!File.Exists(lnkPath) || string.IsNullOrEmpty(appId)) return false;
        bool result = false;
        RunOnSta(() =>
        {
            try
            {
                var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellLink)!)!;
                var persist = (IPersistFile)link;
                persist.Load(lnkPath, 0);

                var store = (IPropertyStore)link;
                var pkey = PKEY_AppUserModel_ID;
                var pv = PROPVARIANT.FromString(appId);
                int hr = store.SetValue(ref pkey, ref pv);
                pv.Clear();
                if (hr >= 0)
                {
                    store.Commit();
                    persist.Save(lnkPath, true);
                    result = true;
                }
                Marshal.ReleaseComObject(store);
                Marshal.ReleaseComObject(link);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JumpListService] SetShortcutAppId on '{lnkPath}' failed: {ex.Message}");
            }
        });
        return result;
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static IShellLinkW CreateTask(string exePath, string args, string title, string? iconPath = null, string? description = null)
    {
        var link = (IShellLinkW)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_ShellLink)!)!;
        link.SetPath(exePath);
        link.SetArguments(args);
        link.SetWorkingDirectory(AppContext.BaseDirectory);
        if (!string.IsNullOrEmpty(description))
            link.SetDescription(description);

        string effectiveIcon = !string.IsNullOrEmpty(iconPath) && File.Exists(iconPath) ? iconPath : exePath;
        link.SetIconLocation(effectiveIcon, 0);

        var store = (IPropertyStore)link;
        var pkey = PKEY_Title;
        var pv = PROPVARIANT.FromString(title);
        store.SetValue(ref pkey, ref pv);
        pv.Clear();
        store.Commit();

        return link;
    }

    public static void UpdateGroupJumpList(AppGroup group)
    {
        if (group == null) return;

        string appId = GetGroupAppId(group.Id);
        if (string.Equals(CurrentProcessAppId, appId, StringComparison.OrdinalIgnoreCase))
        {
            WriteGroupJumpListInProcess(group.Id);
            return;
        }

        SpawnJumpListWriter(group.Id);
    }

    public static void WriteGroupJumpListInProcess(Guid groupId)
    {
        var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null) return;
        WriteGroupJumpListCore(group);
    }

    private static void SpawnJumpListWriter(Guid groupId)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = HostExePath,
                Arguments = $"--group {groupId:D} --update-jumplist",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(8000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JumpListService] SpawnJumpListWriter error: {ex.Message}");
        }
    }

    private static void WriteGroupJumpListCore(AppGroup group)
    {
        RunOnSta(() =>
        {
            try
            {
                string appId = GetGroupAppId(group.Id);
                string host = HostExePath;
                RegisterGroupAumid(appId, group.Name, group.CustomIconPath);

                var destList = (ICustomDestinationList)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_DestinationList)!)!;
                destList.SetAppID(appId);

                Guid iidArray = IID_IObjectArray;
                int hr = destList.BeginList(out uint minSlots, ref iidArray, out object removedObj);
                if (hr < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[JumpListService] BeginList failed hr=0x{hr:X8} for '{group.Name}'");
                    try { destList.AbortList(); } catch { }
                    Marshal.ReleaseComObject(destList);
                    return;
                }

                var removedArgs = CollectRemovedArguments(removedObj);

                var apps = group.Apps
                    .Where(a => !string.IsNullOrWhiteSpace(a.ExePath))
                    .Take(15)
                    .ToList();

                if (apps.Count > 0)
                {
                    var cat = (IObjectCollection)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_EnumerableObjectCollection)!)!;
                    foreach (var app in apps)
                    {
                        string runArgs = $"--group {group.Id:D} --run {QuoteArg(app.ExePath)}";
                        if (!string.IsNullOrWhiteSpace(app.Arguments))
                            runArgs += $" --run-args {QuoteArg(app.Arguments)}";
                        if (app.RunAsAdmin)
                            runArgs += " --run-admin";

                        if (removedArgs.Any(r => r.Contains(app.ExePath, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        string icon = !string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath)
                            ? app.IconPath
                            : (File.Exists(app.ExePath) ? app.ExePath : host);
                        cat.AddObject(CreateTask(host, runArgs, app.Name, icon, app.Name));
                    }

                    cat.GetCount(out uint catCount);
                    if (catCount > 0)
                    {
                        string categoryName = group.GroupType == GroupType.Files ? "Files" : "Apps";
                        int catHr = destList.AppendCategory(categoryName, cat);
                        if (catHr < 0)
                            System.Diagnostics.Debug.WriteLine($"[JumpListService] AppendCategory hr=0x{catHr:X8} for '{group.Name}'");
                    }
                    Marshal.ReleaseComObject(cat);
                }

                var tasks = (IObjectCollection)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_EnumerableObjectCollection)!)!;
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --edit", "Edit app group"));
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --parent-settings", "Edit app group (Parent)"));
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --rename", "Rename app group"));
                int taskHr = destList.AddUserTasks(tasks);
                if (taskHr < 0)
                    System.Diagnostics.Debug.WriteLine($"[JumpListService] AddUserTasks hr=0x{taskHr:X8} for '{group.Name}'");

                destList.CommitList();
                Marshal.ReleaseComObject(tasks);
                Marshal.ReleaseComObject(destList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JumpListService] UpdateGroupJumpList error for '{group.Name}': {ex.Message}");
            }
        });
    }

    /// <summary>
    /// DTM Mode: Injects test jumplist items and categories onto an App Group's taskbar shortcut / AUMID.
    /// </summary>
    public static bool AddTestJumpListToGroup(Guid groupId)
    {
        var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null) return false;

        RunOnSta(() =>
        {
            try
            {
                string appId = GetGroupAppId(group.Id);
                string host = HostExePath;
                RegisterGroupAumid(appId, group.Name, group.CustomIconPath);

                var destList = (ICustomDestinationList)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_DestinationList)!)!;
                destList.SetAppID(appId);

                Guid iidArray = IID_IObjectArray;
                int hr = destList.BeginList(out uint minSlots, ref iidArray, out object removedObj);
                if (hr < 0)
                {
                    try { destList.AbortList(); } catch { }
                    Marshal.ReleaseComObject(destList);
                    return;
                }

                // Add DTM Test Category with interactive tools
                var testCat = (IObjectCollection)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_EnumerableObjectCollection)!)!;
                string calcPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "calc.exe");
                string notepadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
                
                testCat.AddObject(CreateTask(calcPath, "", "🧮 Test Calc (DTM)", null, "Open Calculator"));
                testCat.AddObject(CreateTask(notepadPath, "", "📝 Test Notepad (DTM)", null, "Open Notepad"));
                testCat.AddObject(CreateTask(host, $"--group {group.Id:D}", $"🚀 Launch {group.Name}", group.CustomIconPath));
                
                destList.AppendCategory("⭐ DTM Test Actions", testCat);
                Marshal.ReleaseComObject(testCat);

                // Add Standard tasks + DTM test verification item
                var tasks = (IObjectCollection)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_EnumerableObjectCollection)!)!;
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --edit", "Edit app group"));
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --parent-settings", "Edit app group (Parent)"));
                tasks.AddObject(CreateTask(host, $"--group {group.Id:D} --rename", "Rename app group"));
                tasks.AddObject(CreateTask(host, "", "🧪 DTM JumpList Test OK"));
                
                destList.AddUserTasks(tasks);
                Marshal.ReleaseComObject(tasks);

                destList.CommitList();
                Marshal.ReleaseComObject(destList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JumpListService] AddTestJumpListToGroup error: {ex.Message}");
            }
        });
        return true;
    }

    private static List<string> CollectRemovedArguments(object? removedObj)
    {
        var list = new List<string>();
        if (removedObj == null) return list;
        try
        {
            var array = (IObjectArray)removedObj;
            array.GetCount(out uint count);
            Guid iidLink = typeof(IShellLinkW).GUID;
            for (uint i = 0; i < count; i++)
            {
                try
                {
                    array.GetAt(i, ref iidLink, out object item);
                    if (item is IShellLinkW link)
                    {
                        var sb = new StringBuilder(1024);
                        link.GetArguments(sb, sb.Capacity);
                        list.Add(sb.ToString());
                        Marshal.ReleaseComObject(item);
                    }
                }
                catch { }
            }
        }
        catch { }
        return list;
    }

    public static void UpdateGroupShortcutsAppId(AppGroup group)
    {
        if (group == null) return;
        string appId = GetGroupAppId(group.Id);
        string targetArg = $"--group {group.Id}";

        string[] dirs =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs", "TaskTile"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile", "Shortcuts")
        ];

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) continue;
                dynamic shell = Activator.CreateInstance(shellType)!;
                var lnkFiles = Directory.GetFiles(dir, "TaskTile - *.lnk");
                foreach (var f in lnkFiles)
                {
                    try
                    {
                        dynamic lnk = shell.CreateShortcut(f);
                        string? args = lnk.Arguments as string;
                        Marshal.ReleaseComObject(lnk);

                        if (args != null && args.Contains(targetArg))
                        {
                            SetShortcutAppId(f, appId);
                        }
                    }
                    catch { }
                }
                Marshal.ReleaseComObject(shell);
            }
            catch { }
        }
    }

    public static void RegisterAllJumpLists()
    {
        Task.Run(() =>
        {
            try
            {
                var groups = GroupService.Instance.Groups.ToList();
                foreach (var g in groups)
                {
                    UpdateGroupJumpList(g);
                    UpdateGroupShortcutsAppId(g);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JumpListService] RegisterAllJumpLists error: {ex.Message}");
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (ex != null)
        {
            System.Diagnostics.Debug.WriteLine($"[JumpListService] STA exception: {ex.Message}");
        }
    }
}
