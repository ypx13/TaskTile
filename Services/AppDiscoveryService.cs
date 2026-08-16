using System.Runtime.InteropServices;
using System.Text;
using TaskTile.Models;

namespace TaskTile.Services;

/// <summary>
/// Scans the Windows Start Menu for installed apps.
/// Uses native IShellLink to safely parse shortcuts without throwing 0xc0000005.
/// </summary>
public class AppDiscoveryService
{
    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);

    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOOPENFILEERRORBOX = 0x8000;

    public static List<AppEntry> GetInstalledApps()
    {
        var startMenuPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")
        };

        var results = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);

        // Required for COM
        var thread = new Thread(() =>
        {
            // Suppress "Exception Processing Message 0xc0000005" hard errors for this thread
            uint oldMode = SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX);

            try
            {
                Type shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!;

            foreach (var dir in startMenuPaths)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        var shellLink = (IShellLinkW)Activator.CreateInstance(shellLinkType)!;
                        var persistFile = (System.Runtime.InteropServices.ComTypes.IPersistFile)shellLink;

                        persistFile.Load(lnk, 0);

                        StringBuilder target = new StringBuilder(260);
                        shellLink.GetPath(target, target.Capacity, IntPtr.Zero, 0);

                        string targetPath = target.ToString();
                        string name = Path.GetFileNameWithoutExtension(lnk);

                        Marshal.ReleaseComObject(shellLink);

                        if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath)) continue;
                        if (results.ContainsKey(name)) continue;

                        results[name] = new AppEntry
                        {
                            Name = name,
                            ExePath = targetPath,
                            IconPath = "" // icon shown via FontIcon fallback in UI
                        };
                    }
                    catch { /* skip broken shortcuts quietly */ }
                }
                }
            }
            finally
            {
                // Restore original error mode
                SetErrorMode(oldMode);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return results.Values.OrderBy(a => a.Name).ToList();
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
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
}
