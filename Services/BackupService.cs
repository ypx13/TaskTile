using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TaskTile.Models;

namespace TaskTile.Services
{
    public class BackupOptions
    {
        public bool BackupSettings { get; set; } = true;
        public bool BackupGroups { get; set; } = true;
        public bool BackupLaunchOptions { get; set; } = true;
        public bool BackupIcons { get; set; } = true;
        public List<string> SelectedGroupIds { get; set; } = new();
        public string OutputDirectory { get; set; } = string.Empty;
    }

    public class BackupProgressReport
    {
        public int Step { get; set; } // 0 = Start, 1 = Settings, 2 = Groups, 3 = Assets, 4 = Zipping, 5 = Done
        public string Message { get; set; } = string.Empty;
        public double Percentage { get; set; }
    }

    public class BackupService
    {
        private static BackupService? _instance;
        public static BackupService Instance => _instance ??= new BackupService();

                [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public string GetDefaultBackupDirectory()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string backupDir = Path.Combine(docs, "TaskTile_Backups");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }
            return backupDir;
        }

        public async Task<string> CreateBackupAsync(BackupOptions options, IProgress<BackupProgressReport>? progress = null)
        {
            string backupDir = string.IsNullOrWhiteSpace(options.OutputDirectory)
                ? GetDefaultBackupDirectory()
                : options.OutputDirectory;

            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), $"TaskTile_Backup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempFolder);

            try
            {
                progress?.Report(new BackupProgressReport
                {
                    Step = 1,
                    Message = "Collecting application settings...",
                    Percentage = 15
                });
                await Task.Delay(200);

                if (options.BackupSettings)
                {
                    var settings = SettingsService.Current;
                    string settingsYaml = GenerateSettingsYaml(settings);
                    await File.WriteAllTextAsync(Path.Combine(tempFolder, "tasktile_settings.yaml"), settingsYaml, Encoding.UTF8);

                    // Also include json copy for interoperability
                    string settingsJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(Path.Combine(tempFolder, "tasktile_settings.json"), settingsJson, Encoding.UTF8);
                }

                progress?.Report(new BackupProgressReport
                {
                    Step = 2,
                    Message = "Packaging app groups & launch options...",
                    Percentage = 45
                });
                await Task.Delay(300);

                if (options.BackupGroups)
                {
                    var allGroups = GroupService.Instance.Groups.ToList();
                    var groupsToSave = options.SelectedGroupIds.Count > 0
                        ? allGroups.Where(g => options.SelectedGroupIds.Contains(g.Id.ToString())).ToList()
                        : allGroups;

                    string groupsYaml = GenerateGroupsYaml(groupsToSave);
                    await File.WriteAllTextAsync(Path.Combine(tempFolder, "tasktile_groups.yaml"), groupsYaml, Encoding.UTF8);

                    string groupsJson = JsonSerializer.Serialize(groupsToSave, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(Path.Combine(tempFolder, "tasktile_groups.json"), groupsJson, Encoding.UTF8);

                    // Copy custom icons if present
                    string iconsDir = Path.Combine(tempFolder, "icons");
                    Directory.CreateDirectory(iconsDir);
                    foreach (var grp in groupsToSave)
                    {
                        if (!string.IsNullOrEmpty(grp.CustomIconPath) && File.Exists(grp.CustomIconPath))
                        {
                            try
                            {
                                string dest = Path.Combine(iconsDir, $"{grp.Id}_{Path.GetFileName(grp.CustomIconPath)}");
                                File.Copy(grp.CustomIconPath, dest, true);
                            }
                            catch { }
                        }
                    }
                }

                progress?.Report(new BackupProgressReport
                {
                    Step = 3,
                    Message = "Generating backup manifest...",
                    Percentage = 75
                });
                await Task.Delay(250);

                var manifest = new
                {
                    Application = "TaskTile",
                    CodeName = "TaskTile",
                    Version = "0.7.5",
                    ExportDate = DateTime.Now.ToString("o"),
                    MachineName = Environment.MachineName,
                    IncludesSettings = options.BackupSettings,
                    IncludesGroups = options.BackupGroups
                };
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(tempFolder, "backup_manifest.json"), manifestJson, Encoding.UTF8);

                progress?.Report(new BackupProgressReport
                {
                    Step = 4,
                    Message = "Zipping up backup archive...",
                    Percentage = 90
                });
                await Task.Delay(350);

                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipPath = Path.Combine(backupDir, $"TaskTile_Backup_{timeStamp}.zip");

                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempFolder, zipPath, CompressionLevel.Optimal, false);

                progress?.Report(new BackupProgressReport
                {
                    Step = 5,
                    Message = "Backup archive created successfully!",
                    Percentage = 100
                });
                await Task.Delay(200);

                return zipPath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                }
                catch { }
            }
        }

        public async Task<bool> RestoreBackupAsync(string zipOrYamlPath)
        {
            if (!File.Exists(zipOrYamlPath)) return false;

            string ext = Path.GetExtension(zipOrYamlPath).ToLowerInvariant();
            if (ext == ".zip")
            {
                string tempDir = Path.Combine(Path.GetTempPath(), $"TaskTile_Restore_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempDir);
                try
                {
                    ZipFile.ExtractToDirectory(zipOrYamlPath, tempDir, true);

                    // Restore settings
                    string settingsJson = Path.Combine(tempDir, "tasktile_settings.json");
                    if (File.Exists(settingsJson))
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string destFile = Path.Combine(appData, "TaskTile", "settings.json");
                        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                        File.Copy(settingsJson, destFile, true);
                        SettingsService.Load();
                    }

                    // Restore groups
                    string groupsJson = Path.Combine(tempDir, "tasktile_groups.json");
                    if (File.Exists(groupsJson))
                    {
                        var json = await File.ReadAllTextAsync(groupsJson);
                        var grps = JsonSerializer.Deserialize<List<AppGroup>>(json);
                        if (grps != null)
                        {
                            GroupService.Instance.Groups.Clear();
                            foreach (var g in grps)
                            {
                                GroupService.Instance.Groups.Add(g);
                            }
                            GroupService.Instance.Save();
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Restore failed: {ex.Message}");
                    return false;
                }
                finally
                {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            }
            return false;
        }

        public async Task<RECT> PositionExplorerNextToWindowAsync(string zipFilePath, IntPtr mainWindowHwnd)
        {
            RECT explorerFinalRect = new RECT();
            try
            {
                // 1. Get MainWindow rectangle
                RECT ttRect = new RECT();
                if (mainWindowHwnd != IntPtr.Zero)
                {
                    GetWindowRect(mainWindowHwnd, out ttRect);
                }
                else
                {
                    ttRect.Left = 100;
                    ttRect.Top = 100;
                    ttRect.Right = 900;
                    ttRect.Bottom = 700;
                }

                int ttWidth = Math.Max(ttRect.Right - ttRect.Left, 400);
                int ttHeight = Math.Max(ttRect.Bottom - ttRect.Top, 300);

                // 2. Snapshot existing CabinetWClass windows
                var existingHwnds = new HashSet<IntPtr>();
                EnumWindows((hWnd, lParam) =>
                {
                    var sb = new StringBuilder(256);
                    GetClassName(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == "CabinetWClass")
                    {
                        existingHwnds.Add(hWnd);
                    }
                    return true;
                }, IntPtr.Zero);

                // 3. Launch explorer with /select
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{zipFilePath}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);

                // 4. Poll for the new explorer window (up to 3.5s)
                IntPtr explorerHwnd = IntPtr.Zero;
                for (int i = 0; i < 35; i++)
                {
                    await Task.Delay(100);
                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd)) return true;
                        var sb = new StringBuilder(256);
                        GetClassName(hWnd, sb, sb.Capacity);
                        if (sb.ToString() == "CabinetWClass" && !existingHwnds.Contains(hWnd) && hWnd != mainWindowHwnd)
                        {
                            explorerHwnd = hWnd;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (explorerHwnd != IntPtr.Zero) break;
                }

                if (explorerHwnd == IntPtr.Zero)
                {
                    explorerHwnd = FindExplorerWindow();
                }

                if (explorerHwnd == IntPtr.Zero) return explorerFinalRect;

                // 5. Get Monitor Work Area for accurate side-by-side positioning
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                IntPtr hMon = MonitorFromWindow(mainWindowHwnd != IntPtr.Zero ? mainWindowHwnd : explorerHwnd, MONITOR_DEFAULTTONEAREST);
                RECT rcWork;
                if (hMon != IntPtr.Zero && GetMonitorInfo(hMon, ref mi))
                {
                    rcWork = mi.rcWork;
                }
                else
                {
                    rcWork = new RECT { Left = 0, Top = 0, Right = GetSystemMetrics(SM_CXSCREEN), Bottom = GetSystemMetrics(SM_CYSCREEN) };
                }

                int workW = rcWork.Right - rcWork.Left;
                int workH = rcWork.Bottom - rcWork.Top;

                // Compute dimensions to fit cleanly side-by-side
                int targetWidth = Math.Min(ttWidth, (workW - 24) / 2);
                int targetHeight = Math.Min(ttHeight, workH - 24);
                int targetY = Math.Max(rcWork.Top + 10, Math.Min(ttRect.Top, rcWork.Bottom - targetHeight - 10));

                int startX, targetX;
                if (ttRect.Right + targetWidth + 12 <= rcWork.Right)
                {
                    // TaskTile is on the left; Explorer drags to the right
                    startX = ttRect.Left;
                    targetX = ttRect.Right + 12;
                }
                else if (ttRect.Left - targetWidth - 12 >= rcWork.Left)
                {
                    // TaskTile is on the right; Explorer drags to the left
                    startX = ttRect.Left;
                    targetX = ttRect.Left - targetWidth - 12;
                }
                else
                {
                    // Center/wide TaskTile: move TaskTile to left half, Explorer to right half
                    int taskTileX = rcWork.Left + 10;
                    startX = taskTileX;
                    targetX = taskTileX + targetWidth + 12;

                    if (mainWindowHwnd != IntPtr.Zero)
                    {
                        SetWindowPos(mainWindowHwnd, IntPtr.Zero, taskTileX, targetY, targetWidth, targetHeight, SWP_NOACTIVATE | SWP_NOZORDER);
                    }
                }

                // Place Explorer directly behind TaskTile initially (same size)
                SetWindowPos(explorerHwnd, mainWindowHwnd, startX, targetY, targetWidth, targetHeight, SWP_NOACTIVATE | SWP_SHOWWINDOW);

                // Smoothly drag Explorer out next to TaskTile over 20 frames (320ms)
                int frames = 20;
                for (int f = 1; f <= frames; f++)
                {
                    double t = (double)f / frames;
                    double ease = 1.0 - Math.Pow(1.0 - t, 3); // Ease-out cubic
                    int curX = (int)Math.Round(startX + (targetX - startX) * ease);

                    SetWindowPos(explorerHwnd, IntPtr.Zero, curX, targetY, targetWidth, targetHeight, SWP_NOACTIVATE | SWP_NOZORDER);
                    await Task.Delay(16);
                }

                // Final position and activate explorer
                SetWindowPos(explorerHwnd, IntPtr.Zero, targetX, targetY, targetWidth, targetHeight, SWP_SHOWWINDOW);
                SetForegroundWindow(explorerHwnd);

                explorerFinalRect = new RECT { Left = targetX, Top = targetY, Right = targetX + targetWidth, Bottom = targetY + targetHeight };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to position explorer: {ex.Message}");
            }
            return explorerFinalRect;
        }

        private IntPtr FindExplorerWindow()
        {
            IntPtr found = IntPtr.Zero;
            DateTime now = DateTime.UtcNow;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                var sb = new StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);
                string cls = sb.ToString();

                if (cls == "CabinetWClass")
                {
                    found = hWnd;
                    return false; // Stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private string GenerateSettingsYaml(AppSettings s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# TaskTile Settings Configuration");
            sb.AppendLine("# Application: TaskTile (Code-name: shifu)");
            sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("tasktile_settings:");
            sb.AppendLine($"  theme: {s.Theme} # 0=System, 1=Light, 2=Dark");
            sb.AppendLine($"  backdrop_style: {s.BackdropStyle} # 0=Mica, 1=MicaAlt, 2=Acrylic, 3=None");
            sb.AppendLine($"  launch_side: {s.LaunchSide}");
            sb.AppendLine($"  running_indicator_style: {s.RunningIndicatorStyle} # 0=Bar, 1=Corner Dot");
            sb.AppendLine($"  disable_animation: {s.DisableAnimation.ToString().ToLower()}");
            sb.AppendLine($"  enable_app_entrance_animation: {s.EnableAppEntranceAnimation.ToString().ToLower()}");
            sb.AppendLine($"  adaptive_refresh_rate_animation: {s.AdaptiveRefreshRateAnimation.ToString().ToLower()}");
            sb.AppendLine($"  animation_speed_multiplier: {s.AnimationSpeedMultiplier}");
            sb.AppendLine($"  disable_auto_hide: {s.DisableAutoHide.ToString().ToLower()}");
            sb.AppendLine($"  disable_float: {s.DisableFloat.ToString().ToLower()}");
            sb.AppendLine($"  disable_rounded_corners: {s.DisableRoundedCorners.ToString().ToLower()}");
            sb.AppendLine($"  taskbar_gap: {s.TaskbarGap}");
            sb.AppendLine($"  title_alignment: {s.TitleAlignment}");
            sb.AppendLine($"  group_card_style: {s.GroupCardStyle}");
            sb.AppendLine($"  start_popups_in_background: {s.StartPopupsInBackground.ToString().ToLower()}");
            sb.AppendLine($"  run_at_startup: {s.RunAtStartup.ToString().ToLower()}");
            sb.AppendLine($"  suppress_explorer: {s.SuppressExplorer.ToString().ToLower()}");
            sb.AppendLine($"  apply_global_config_to_popups: {s.ApplyGlobalConfigToPopups.ToString().ToLower()}");
            sb.AppendLine($"  enable_tray_icon: {s.EnableTrayIcon.ToString().ToLower()}");
            return sb.ToString();
        }

        private string GenerateGroupsYaml(List<AppGroup> groups)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# TaskTile App Groups Configuration");
            sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("app_groups:");
            foreach (var g in groups)
            {
                sb.AppendLine($"  - id: \"{g.Id}\"");
                sb.AppendLine($"    name: \"{EscapeYaml(g.Name)}\"");
                sb.AppendLine($"    shortcut: \"{EscapeYaml(g.Shortcut)}\"");
                sb.AppendLine($"    popup_style: {g.PopupStyle} # 0=Classic, 1=Compact, 2=Modern, 3=List, 4=Card");
                sb.AppendLine($"    running_indicator_style: {g.RunningIndicatorStyle} # -1=Global, 0=Bar, 1=Corner Dot");
                sb.AppendLine($"    theme_override: {g.ThemeOverride}");
                sb.AppendLine($"    backdrop_style: {g.BackdropStyle}");
                sb.AppendLine($"    title_alignment: {g.TitleAlignment}");
                sb.AppendLine($"    tile_spacing: {g.TileSpacing}");
                sb.AppendLine($"    taskbar_offset: {g.TaskbarOffset}");
                sb.AppendLine($"    is_pinned: {g.IsPinned.ToString().ToLower()}");
                sb.AppendLine($"    custom_icon_path: \"{EscapeYaml(g.CustomIconPath)}\"");
                sb.AppendLine("    apps:");
                foreach (var a in g.Apps)
                {
                    sb.AppendLine($"      - name: \"{EscapeYaml(a.Name)}\"");
                    sb.AppendLine($"        exe_path: \"{EscapeYaml(a.ExePath)}\"");
                    sb.AppendLine($"        arguments: \"{EscapeYaml(a.Arguments)}\"");
                    sb.AppendLine($"        run_as_admin: {a.RunAsAdmin.ToString().ToLower()}");
                    sb.AppendLine($"        tile_size: {a.TileSize} # 1=1x1, 2=2x1, 3=1x2, 4=2x2");
                }
            }
            return sb.ToString();
        }

        private string EscapeYaml(string? val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
