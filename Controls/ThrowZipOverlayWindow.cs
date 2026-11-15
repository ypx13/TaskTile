using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TaskTile.Controls
{

    public sealed class ThrowZipOverlayWindow
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
            public POINT(int x, int y) { this.x = x; this.y = y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
            public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint ULW_ALPHA = 0x00000002;

        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_NOACTIVATE = 0x08000000;
        private const uint WS_POPUP = 0x80000000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        private readonly int _startX;
        private readonly int _startY;
        private readonly int _endX;
        private readonly int _endY;

        public ThrowZipOverlayWindow(int startScreenX, int startScreenY, int endScreenX, int endScreenY)
        {
            _startX = startScreenX;
            _startY = startScreenY;
            _endX = endScreenX;
            _endY = endScreenY;
        }

        public void Activate()
        {
            _ = ThrowAsync(_startX, _startY, _endX, _endY);
        }

        public static async Task ThrowAsync(int startX, int startY, int targetX, int targetY)
        {
            IntPtr hwnd = IntPtr.Zero;
            try
            {
                hwnd = CreateWindowEx(
                    WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                    "Static",
                    "TaskTileZipThrow",
                    WS_POPUP,
                    startX - 24, startY - 24, 48, 48,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                if (hwnd == IntPtr.Zero) return;

                int frames = 32;
                for (int f = 0; f <= frames; f++)
                {
                    double t = (double)f / frames
                    double ease = t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2.0) / 2.0;

                    int curX = (int)Math.Round(startX + (targetX - startX) * ease) - 24;
                    int curY = (int)Math.Round(startY + (targetY - startY) * ease - Math.Sin(t * Math.PI) * 120) - 24;

                    double angle = t * 540.0;ions
                    double scale = t < 0.5 ? 1.0 + t * 0.4 : 1.2 - (t - 0.5) * 0.4;

                    using var bmp = CreateZipBitmap(angle, scale);
                    ApplyBitmapToLayeredWindow(hwnd, bmp, 255);

                    SetWindowPos(hwnd, HWND_TOPMOST, curX, curY, 48, 48, SWP_NOACTIVATE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    await Task.Delay(16);
                }

                for (int f = 1; f <= 5; f++)
                {
                    byte alpha = (byte)Math.Max(0, 255 - f * 50);
                    using var bmp = CreateZipBitmap(0, 1.0 + (5 - f) * 0.04);
                    ApplyBitmapToLayeredWindow(hwnd, bmp, alpha);
                    await Task.Delay(20);
                }
            }
            catch
            {
                // Safety catch
            }
            finally
            {
                if (hwnd != IntPtr.Zero)
                {
                    DestroyWindow(hwnd);
                }
            }
        }

        private static Bitmap CreateZipBitmap(double angleDegrees, double scale)
        {
            var bmp = new Bitmap(48, 48, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            g.TranslateTransform(24f, 24f);
            g.RotateTransform((float)angleDegrees);
            g.ScaleTransform((float)scale, (float)scale);
            g.TranslateTransform(-24f, -24f);

            using (var shadowBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            {
                FillRoundedRectangle(g, shadowBrush, new Rectangle(6, 6, 36, 36), 8);
            }

            using (var badgeBrush = new LinearGradientBrush(
                new Point(0, 5), new Point(0, 41),
                Color.FromArgb(255, 0, 120, 215),
                Color.FromArgb(255, 0, 90, 180)))
            {
                FillRoundedRectangle(g, badgeBrush, new Rectangle(5, 5, 36, 36), 8);
            }

            using (var borderPen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f))
            {
                DrawRoundedRectangle(g, borderPen, new Rectangle(5, 5, 36, 36), 8);
            }

            using (var zipPen = new Pen(Color.White, 1.5f))
            {
                g.DrawLine(zipPen, 23, 8, 23, 23);
                g.DrawLine(zipPen, 25, 8, 25, 23);
                for (int y = 9; y <= 22; y += 3)
                {
                    g.DrawLine(zipPen, 21, y, 27, y);
                }
            }

            using (var pullBrush = new SolidBrush(Color.FromArgb(255, 255, 205, 0)))
            {
                g.FillRectangle(pullBrush, 21, 23, 6, 5);
            }

            using (var textFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("ZIP", textFont, textBrush, new RectangleF(5, 27, 36, 12), sf);
            }

            return bmp;
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle bounds, int cornerRadius)
        {
            using var path = CreateRoundedRectanglePath(bounds, cornerRadius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle bounds, int cornerRadius)
        {
            using var path = CreateRoundedRectanglePath(bounds, cornerRadius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void ApplyBitmapToLayeredWindow(IntPtr hwnd, Bitmap bmp, byte opacity)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr oldBmp = SelectObject(memDc, hBitmap);

            POINT pSrc = new POINT(0, 0);
            SIZE size = new SIZE(bmp.Width, bmp.Height);
            BLENDFUNCTION blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = opacity,
                AlphaFormat = AC_SRC_ALPHA
            };

            UpdateLayeredWindow(hwnd, screenDc, IntPtr.Zero, ref size, memDc, ref pSrc, 0, ref blend, ULW_ALPHA);

            SelectObject(memDc, oldBmp);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
