using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskTile.Helpers {
    public class SystemTrayManager {
        private static IntPtr _hwnd = IntPtr.Zero;
        private static IntPtr _hIcon = IntPtr.Zero;
        private static IntPtr _hMenu = IntPtr.Zero;
        private static NativeMethods.WndProcDelegate _wndProcDelegate;
        private static Action _onShowCallback;
        private static Action _onExitCallback;
        private static bool _isInitialized = false;
        private static bool _isVisible = false;
        private static int WM_TASKBARCREATED;
        private const string WndClassName = "WinUI3AppGroupTrayWndClass";

        // ── Public API ────────────────────────────────────────────────────────

        public static void Initialize(Action showCallback, Action exitCallback) {
            _onShowCallback = showCallback;
            _onExitCallback = exitCallback;
            _isInitialized = true;

            WM_TASKBARCREATED = NativeMethods.RegisterWindowMessage("TaskbarCreated");

            InitializeBasedOnSettings();
        }

        private static void InitializeBasedOnSettings() {
            try {
                if (TaskTile.Services.SettingsService.Current.EnableTrayIcon)
                    ShowSystemTray();
            }
            catch (Exception ex) {
                Debug.WriteLine($"Error loading settings for system tray: {ex.Message}");
                ShowSystemTray();
            }
        }

        public static void ShowSystemTray() {
            if (!_isInitialized) return;
            if (_isVisible) return;

            EnsureWindow();
            CreateTrayIcon();
            _isVisible = true;
        }

        public static void HideSystemTray() {
            if (!_isVisible) return;

            // Remove icon only — keep window alive so message pump stays intact
            var nid = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);

            if (_hIcon != IntPtr.Zero) {
                NativeMethods.DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            if (_hMenu != IntPtr.Zero) {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }

            _isVisible = false;
        }

        public static void Cleanup() {
            HideSystemTray();

            if (_hwnd != IntPtr.Zero) {
                NativeMethods.DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            _isVisible = false;
        }

        public static void UpdateTooltip(string tooltip) {
            if (_hwnd == IntPtr.Zero || !_isVisible) return;

            var nid = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NativeMethods.NIF_TIP,
                szTip = tooltip
            };
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
        }

        // ── Window ────────────────────────────────────────────────────────────

        private static void EnsureWindow() {
            if (_hwnd != IntPtr.Zero) return;

            // Create delegate fresh — keep reference in static field to prevent GC
            _wndProcDelegate = new NativeMethods.WndProcDelegate(WndProc);

            var wc = new NativeMethods.WNDCLASSEX {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = NativeMethods.GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = NativeMethods.LoadCursor(IntPtr.Zero, 32512u),
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = WndClassName,
                hIconSm = IntPtr.Zero
            };

            NativeMethods.RegisterClassEx(ref wc);

            _hwnd = NativeMethods.CreateWindowEx(
                0, WndClassName, "WinUI3 AppGroup Tray Window",
                0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero,
                NativeMethods.GetModuleHandle(null), IntPtr.Zero);
        }

        // ── Icon & Menu ───────────────────────────────────────────────────────

        private static void CreateTrayIcon() {
            // Load icon fresh each time
            if (_hIcon == IntPtr.Zero) {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                var iconPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(exePath ?? "") ?? "", "Assets", "newicon.ico");
                if (System.IO.File.Exists(iconPath)) {
                    _hIcon = NativeMethods.LoadImage(IntPtr.Zero, iconPath, 
                        NativeMethods.IMAGE_ICON, 0, 0, NativeMethods.LR_LOADFROMFILE);
                }
                if (_hIcon == IntPtr.Zero) {
                    _hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
                }
            }

            var nid = new NativeMethods.NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = NativeMethods.WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "TaskTile"
            };

            bool ok = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid);
            if (!ok) {
                // May already exist — try modify
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
            }
            CreateMenu();
        }

        public static void RebuildMenu() {
            if (_hMenu != IntPtr.Zero) {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }
            CreateMenu();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string iconPath, out ushort piIcon);

        private static void CreateMenu() {
            if (_hMenu == IntPtr.Zero) {
                _hMenu = NativeMethods.CreatePopupMenu();
                NativeMethods.AppendMenu(_hMenu, 0, 1001, "Show TaskTile");
                NativeMethods.AppendMenu(_hMenu, 0, 1002, "Settings");
                NativeMethods.AppendMenu(_hMenu, NativeMethods.MF_SEPARATOR, 0, string.Empty);
                
                // Add dynamic groups
                int id = 2000;
                var groups = TaskTile.Services.GroupService.Instance.Groups;
                foreach (var group in groups) {
                    NativeMethods.AppendMenu(_hMenu, 0, (uint)id++, group.Name);
                }

                if (groups.Count > 0) {
                    NativeMethods.AppendMenu(_hMenu, NativeMethods.MF_SEPARATOR, 0, string.Empty);
                }

                NativeMethods.AppendMenu(_hMenu, 0, 1003, "Restart");
                NativeMethods.AppendMenu(_hMenu, 0, 1004, "Exit");
            }
        }

        // ── WndProc ───────────────────────────────────────────────────────────

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) {
            if ((int)msg == WM_TASKBARCREATED && _isVisible) {
                // Explorer restarted — recreate icon
                _hIcon = IntPtr.Zero;
                CreateTrayIcon();
                return IntPtr.Zero;
            }

            switch (msg) {
                case NativeMethods.WM_TRAYICON:
                    HandleTrayIconMessage(lParam);
                    break;

                case NativeMethods.WM_COMMAND:
                    HandleMenuCommand(wParam.ToInt32() & 0xFFFF);
                    break;
            }

            return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private static void HandleTrayIconMessage(IntPtr lParam) {
            switch (lParam.ToInt32()) {
                case (int)NativeMethods.WM_LBUTTONDBLCLK:
                    _onShowCallback?.Invoke();
                    break;

                case (int)NativeMethods.WM_RBUTTONUP:
                    ShowContextMenu();
                    break;
            }
        }

        private static void HandleMenuCommand(int command) {
            if (command == 1001) _onShowCallback?.Invoke();
            else if (command == 1002) App.MainWindowInstance?.ShowSettings();
            else if (command == 1003) {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                Microsoft.UI.Xaml.Application.Current.Exit();
            }
            else if (command == 1004) _onExitCallback?.Invoke();
            else if (command >= 2000) {
                int index = command - 2000;
                var groups = TaskTile.Services.GroupService.Instance.Groups;
                if (index >= 0 && index < groups.Count) {
                    App.MainWindowInstance?.ShowGroup(groups[index].Id.ToString());
                }
            }
        }

        private static void ShowContextMenu() {
            if (_hMenu == IntPtr.Zero) return;

            NativeMethods.GetCursorPos(out NativeMethods.POINT pt);
            NativeMethods.SetForegroundWindow(_hwnd);

            uint result = NativeMethods.TrackPopupMenu(
                _hMenu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

            NativeMethods.PostMessage(_hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (result != 0)
                HandleMenuCommand((int)result);
        }
    }
}