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

    public static void PinGroup(AppGroup group)
    {
        System.Threading.Tasks.Task.Run(() => {
            var lnkPath = Path.Combine(DesktopDir, $"TaskTile - {group.Name}.lnk");
            string? iconPath = GenerateGroupIcon(group, out string? _);

            RunOnSta(() => CreateShortcut(lnkPath,
                LauncherExe, $"--group {group.Id}", group.Name, AppContext.BaseDirectory, iconPath));

            // Highlight the newly created shortcut in Windows Explorer so user can manually pin it
            try
            {
                if (!SettingsService.Current.SuppressExplorer)
                {
                    Process.Start("explorer.exe", $"/select,\"{lnkPath}\"");
                }
            }
            catch { }
        });
    }

    public static void UnpinGroup(AppGroup group)
    {
        var lnkPath = Path.Combine(DesktopDir, $"TaskTile - {group.Name}.lnk");
        if (!File.Exists(lnkPath)) return;

        try { File.Delete(lnkPath); } catch { }
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
                // Determine folder background based on Style Enum (0=Normal, 1=Transparent, 2=UWP Style)
                System.Drawing.Color bgColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); // Default Normal Dark
                if (group.FolderIconStyle == 1)
                    bgColor = System.Drawing.Color.Transparent;
                else if (group.FolderIconStyle == 2)
                {
                    var uiSettings = new Windows.UI.ViewManagement.UISettings();
                    var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                    bgColor = System.Drawing.Color.FromArgb(255, accent.R, accent.G, accent.B);
                }

                if (group.FolderIconStyle != 1)
                {
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

                    using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(100, 255, 255, 255), 1);
                    g.DrawPath(borderPen, path);
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
                
                // Adjust if transparent bg
                int innerPadding = group.FolderIconStyle == 1 ? 0 : 2;
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
