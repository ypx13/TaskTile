using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace TaskTile.Services
{
    public static class IconHelper
    {
        private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile", "Icons");

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        public static Bitmap? ExtractFolderIcon()
        {
            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                IntPtr hImg = SHGetFileInfo("folder", FILE_ATTRIBUTE_DIRECTORY, out shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    using var icon = Icon.FromHandle(shinfo.hIcon);
                    var bmp = new Bitmap(icon.ToBitmap());
                    DestroyIcon(shinfo.hIcon);
                    return bmp;
                }
            }
            catch { }
            return null;
        }

        public static string GetOrExtractIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "";

            bool isDir = Directory.Exists(filePath);
            if (!isDir && !File.Exists(filePath)) return "";

            string hash = ComputeHash(filePath);
            string name = isDir ? Path.GetFileName(filePath.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(name)) name = "folder";
            string cachedPath = Path.Combine(CacheDir, $"{name}_{hash}.png");

            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            try
            {
                Directory.CreateDirectory(CacheDir);
                Bitmap? iconBitmap = null;

                if (isDir)
                {
                    iconBitmap = ExtractFolderIcon();
                }
                else if (Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    iconBitmap = ExtractWindowsAppIcon(filePath);
                    if (iconBitmap == null)
                    {
                        try
                        {
                            dynamic shell = Microsoft.VisualBasic.Interaction.CreateObject("WScript.Shell");
                            dynamic shortcut = shell.CreateShortcut(filePath);
                            string iconPath = shortcut.IconLocation;
                            string targetPath = shortcut.TargetPath;

                            if (!string.IsNullOrEmpty(iconPath) && iconPath != ",")
                            {
                                string[] iconInfo = iconPath.Split(',');
                                string actualIconPath = iconInfo[0].Trim();
                                int iconIndex = iconInfo.Length > 1 ? int.Parse(iconInfo[1].Trim()) : 0;
                                if (File.Exists(actualIconPath))
                                {
                                    iconBitmap = ExtractSpecificIcon(actualIconPath, iconIndex);
                                }
                            }

                            if (iconBitmap == null && !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                            {
                                iconBitmap = ExtractIconWithoutArrow(targetPath);
                            }

                            if (iconBitmap == null)
                            {
                                var icon = Icon.ExtractAssociatedIcon(filePath);
                                if (icon != null) iconBitmap = icon.ToBitmap();
                            }
                        }
                        catch { }
                    }
                }
                else
                {
                    iconBitmap = ExtractIconWithoutArrow(filePath);
                }

                if (iconBitmap != null)
                {
                    iconBitmap.Save(cachedPath, ImageFormat.Png);
                    iconBitmap.Dispose();
                    return cachedPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
            }

            return "";
        }

        private static Bitmap? ExtractSpecificIcon(string iconPath, int iconIndex)
        {
            try
            {
                IntPtr[] hIcons = new IntPtr[1];
                uint iconCount = ExtractIconEx(iconPath, iconIndex, hIcons, null!, 1);

                if (iconCount > 0 && hIcons[0] != IntPtr.Zero)
                {
                    using var icon = Icon.FromHandle(hIcons[0]);
                    var bitmap = new Bitmap(icon.ToBitmap());
                    DestroyIcon(hIcons[0]);
                    return bitmap;
                }
            }
            catch { }
            return null;
        }

        private static Bitmap? ExtractIconWithoutArrow(string targetPath)
        {
            try
            {
                IntPtr[] hIcons = new IntPtr[1];
                uint iconCount = ExtractIconEx(targetPath, 0, hIcons, null!, 1);

                if (iconCount > 0 && hIcons[0] != IntPtr.Zero)
                {
                    using var icon = Icon.FromHandle(hIcons[0]);
                    var bitmap = new Bitmap(icon.ToBitmap());
                    DestroyIcon(hIcons[0]);
                    return bitmap;
                }

                return Icon.ExtractAssociatedIcon(targetPath)?.ToBitmap();
            }
            catch { }
            return null;
        }

        private static Bitmap? ExtractWindowsAppIcon(string shortcutPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return null;

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic folder = shell.Namespace(Path.GetDirectoryName(shortcutPath));
                dynamic shortcutItem = folder.ParseName(Path.GetFileName(shortcutPath));

                string linkTarget = "";
                object? nullObj = null;
                for (int i = 0; i < 500; i++)
                {
                    string propertyName = folder.GetDetailsOf(nullObj, i);
                    if (propertyName == "Link target")
                    {
                        linkTarget = folder.GetDetailsOf(shortcutItem, i);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(linkTarget)) return null;

                string appName = System.Text.RegularExpressions.Regex.Replace(linkTarget, "_.*$", "");
                if (string.IsNullOrEmpty(appName)) return null;

                Windows.Management.Deployment.PackageManager packageManager = new Windows.Management.Deployment.PackageManager();
                var packages = packageManager.FindPackagesForUser("");
                var appPackage = packages.FirstOrDefault(p => p.Id.Name.StartsWith(appName, StringComparison.OrdinalIgnoreCase));

                if (appPackage == null) return null;

                string installPath = appPackage.InstalledLocation.Path;
                string manifestPath = Path.Combine(installPath, "AppxManifest.xml");

                if (!File.Exists(manifestPath)) return null;

                XmlDocument manifest = new XmlDocument();
                manifest.Load(manifestPath);

                XmlNamespaceManager nsManager = new XmlNamespaceManager(manifest.NameTable);
                nsManager.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                XmlNode? logoNode = manifest.SelectSingleNode("/ns:Package/ns:Properties/ns:Logo", nsManager);
                if (logoNode == null) return null;

                string logoPath = logoNode.InnerText;
                string logoDir = Path.Combine(installPath, Path.GetDirectoryName(logoPath) ?? "");

                if (!Directory.Exists(logoDir)) return null;

                string? highestResLogoPath = null;
                long highestSize = 0;

                foreach (string file in Directory.GetFiles(logoDir, "*StoreLogo*.png", SearchOption.AllDirectories))
                {
                    FileInfo fileInfo = new FileInfo(file);
                    if (fileInfo.Length > highestSize)
                    {
                        highestSize = fileInfo.Length;
                        highestResLogoPath = file;
                    }
                }

                if (highestResLogoPath == null || !File.Exists(highestResLogoPath)) return null;

                using var stream = new FileStream(highestResLogoPath, FileMode.Open, FileAccess.Read);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeHash(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }
    }
}
