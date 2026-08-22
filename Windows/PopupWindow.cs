using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using WinRT.Interop;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Input;

namespace TaskTile.Popups;

// ─── ViewModel ────────────────────────────────────────────────────────────────
public class AppEntryViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private ImageSource? _iconImage;
    private string _name = string.Empty;
    private string _exePath = string.Empty;
    private Visibility _labelVisibility = Visibility.Visible;
    private Visibility _normalVisibility = Visibility.Visible;
    private Visibility _monotoneVisibility = Visibility.Collapsed;
    private Brush _uwpBackground = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    private CornerRadius _tileCornerRadius = new CornerRadius(10);

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name))); } }
    }
    public string ExePath
    {
        get => _exePath;
        set { if (_exePath != value) { _exePath = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ExePath))); } }
    }
    public ImageSource? IconImage
    {
        get => _iconImage;
        set { if (_iconImage != value) { _iconImage = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IconImage))); } }
    }
    public Visibility NormalVisibility
    {
        get => _normalVisibility;
        set { if (_normalVisibility != value) { _normalVisibility = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NormalVisibility))); } }
    }
    public Visibility MonotoneVisibility
    {
        get => _monotoneVisibility;
        set { if (_monotoneVisibility != value) { _monotoneVisibility = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MonotoneVisibility))); } }
    }
    public Visibility LabelVisibility
    {
        get => _labelVisibility;
        set { if (_labelVisibility != value) { _labelVisibility = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LabelVisibility))); } }
    }
    public Brush UwpBackground
    {
        get => _uwpBackground;
        set { if (_uwpBackground != value) { _uwpBackground = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(UwpBackground))); } }
    }
    public CornerRadius TileCornerRadius
    {
        get => _tileCornerRadius;
        set { if (_tileCornerRadius != value) { _tileCornerRadius = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileCornerRadius))); } }
    }
    private TextTrimming _labelTrimming = TextTrimming.CharacterEllipsis;
    public TextTrimming LabelTrimming
    {
        get => _labelTrimming;
        set { if (_labelTrimming != value) { _labelTrimming = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LabelTrimming))); } }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

// Start Menu-style acrylic (matches AcrylicBackgroundFillColorBaseBrush exactly)
public class StartMenuAcrylicBackdrop : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController? _ctrl; SystemBackdropConfiguration? _cfg;
    bool _isDark; float _tintOp; float _lumOp;
    public StartMenuAcrylicBackdrop(bool isDark = true, float tintOp = 0.05f, float lumOp = 0.10f) { _isDark = isDark; _tintOp = tintOp; _lumOp = lumOp; }

    protected override void OnTargetConnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t, Microsoft.UI.Xaml.XamlRoot r)
    {
        base.OnTargetConnected(t, r);
        var tint = _isDark
            ? Windows.UI.Color.FromArgb(255, 31,  31,  31)
            : Windows.UI.Color.FromArgb(255, 242, 242, 242);
        _ctrl = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController
        {
            TintColor            = tint,
            TintOpacity          = _tintOp,
            LuminosityOpacity    = _lumOp,
            FallbackColor        = tint,
        };
        _cfg = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration 
        { 
            IsInputActive = true,
            Theme = _isDark ? Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark : Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light
        };
        _ctrl.AddSystemBackdropTarget(t);
        _ctrl.SetSystemBackdropConfiguration(_cfg);
    }
    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t)
    { base.OnTargetDisconnected(t); _ctrl?.RemoveSystemBackdropTarget(t); _ctrl?.Dispose(); _ctrl = null; }
}

// ─── Backdrops kept for Mica and Dim-overlay ──────────────────────────────────
public class MicaBackdropAlways : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    MicaController? _ctrl; SystemBackdropConfiguration? _cfg;
    bool _isDark; float _tintOp; float _lumOp;
    MicaKind _kind;
    
    public MicaBackdropAlways(bool isDark, MicaKind kind = MicaKind.Base) { _isDark = isDark; _kind = kind; }
    
    protected override void OnTargetConnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t, XamlRoot r)
    {
        base.OnTargetConnected(t, r);
        _ctrl = new MicaController { Kind = _kind };
        _cfg = new SystemBackdropConfiguration 
        { 
            IsInputActive = true,
            Theme = _isDark ? SystemBackdropTheme.Dark : SystemBackdropTheme.Light
        };
        _ctrl.AddSystemBackdropTarget(t); _ctrl.SetSystemBackdropConfiguration(_cfg);
    }
    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t)
    { base.OnTargetDisconnected(t); _ctrl?.RemoveSystemBackdropTarget(t); _ctrl?.Dispose(); _ctrl = null; }
}
public class DimBlurBackdrop : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    DesktopAcrylicController? _ctrl; SystemBackdropConfiguration? _cfg;
    protected override void OnTargetConnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t, XamlRoot r)
    {
        base.OnTargetConnected(t, r);
        _ctrl = new DesktopAcrylicController { TintColor = Windows.UI.Color.FromArgb(255, 0, 0, 0), TintOpacity = 0.6f, LuminosityOpacity = 0.8f, FallbackColor = Windows.UI.Color.FromArgb(255, 0, 0, 0) };
        _cfg = new SystemBackdropConfiguration { IsInputActive = true };
        _ctrl.AddSystemBackdropTarget(t); _ctrl.SetSystemBackdropConfiguration(_cfg);
    }
    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop t)
    { base.OnTargetDisconnected(t); _ctrl?.RemoveSystemBackdropTarget(t); _ctrl?.Dispose(); _ctrl = null; }
}

// ─── PopupWindow ──────────────────────────────────────────────────────────────
public sealed partial class PopupWindow : Window
{
    // Win32
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool GetCursorPos(out global::Windows.Graphics.PointInt32 p);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint f);
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr h, int i, int v);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("dwmapi.dll")]
    static extern int DwmFlush();
    [DllImport("Shell32.dll", CharSet=CharSet.Auto)]
    static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr h);
    [DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public Windows.Graphics.RectInt32 rc;
        public IntPtr lParam;
    }
    [DllImport("shell32.dll")]
    static extern nint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);
    const int ABM_GETSTATE = 0x00000004;
    const int ABS_AUTOHIDE = 0x00000001;

    const int    GWL_STYLE       = -16;
    const int    GWL_EXSTYLE     = -20;
    const int    WS_EX_TOOLWINDOW= 0x00000080;
    const int    WS_EX_APPWINDOW = 0x00040000;
    const int    WS_CAPTION      = 0x00C00000;
    const int    WS_THICKFRAME   = 0x00040000;
    const int    WS_SYSMENU      = 0x00080000;
    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint   SWP_NOMOVE      = 0x0002;
    const uint   SWP_NOSIZE      = 0x0001;
    const uint   SWP_NOZORDER    = 0x0004;
    const uint   SWP_NOACTIVATE  = 0x0010;

    const int    SM_XVIRTUALSCREEN  = 76;
    const int    SM_YVIRTUALSCREEN  = 77;
    const int    SM_CXVIRTUALSCREEN = 78;
    const int    SM_CYVIRTUALSCREEN = 79;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWA_VISIBLE_FRAME_BORDER_THICKNESS = 37;
    const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
    const int DWMWCP_ROUND      = 2;
    const int DWMWCP_ROUNDSMALL = 3;
    const int DWMWA_CLOAK = 14;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("comctl32.dll", SetLastError = true)]
    static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, uint uIdSubclass, IntPtr dwRefData);
    [DllImport("comctl32.dll")]
    static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
    delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    struct MINMAXINFO {
        public global::Windows.Graphics.PointInt32 ptReserved;
        public global::Windows.Graphics.PointInt32 ptMaxSize;
        public global::Windows.Graphics.PointInt32 ptMaxPosition;
        public global::Windows.Graphics.PointInt32 ptMinTrackSize;
        public global::Windows.Graphics.PointInt32 ptMaxTrackSize;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    RECT _lastTrayRect;

    DispatcherTimer _taskbarTracker = null!;

    // Classic paging
    List<List<AppEntryViewModel>> _classicPages = new();
    int             _classicPage = 0;

    string _groupId;
    int _backdropStyle = 0;
    bool _overrideBorderColor = false;
    string _customBorderColor = "#777777";
    bool _activated;
    bool _disableAutoHide = false;
    bool _disableAnimation = false;
    bool _makeMainFocus;
    bool _disableRoundedCorners = false;
    bool _disableFloat = false;
    bool _popupIsDark = false;
    bool _keepOpen = false;
    private bool _marqueeAppLabels = false;
    private bool _scrollAppLabels = false;
    private bool _isDesktopMode;
    private int _physW;
    private int _physH_original;
    private int _targetX, _targetY, _animStartX, _animStartY;
    SubclassProc? _subclassDelegate;

    // max visible apps per classic page
    const int PAGE_SIZE = 6;

    public PopupWindow(string groupId)
    {
        _groupId = groupId;
        try { 
            ExtendsContentIntoTitleBar = true; 
            if (AppWindow.TitleBar != null) AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        } catch { }
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _subclassDelegate = new SubclassProc(WindowSubclass);
        SetWindowSubclass(hwnd, _subclassDelegate, 1, IntPtr.Zero);

        // Disable resize, remove titlebar and border, hide from taskbar
        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.IsResizable   = true; // REQUIRED for native Windows 11 DWM rounded corners
            op.IsMaximizable = false;
            op.IsMinimizable = false;
            op.IsAlwaysOnTop = true;
            op.SetBorderAndTitleBar(true, false); // REQUIRED for SystemBackdrop to not fail and turn black
        }
        AppWindow.IsShownInSwitchers = false;
        
        // Set Window Icon
        try
        {
            var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "newicon.png");
            if (System.IO.File.Exists(iconPath))
            {
                var bmp = new System.Drawing.Bitmap(iconPath);
                var iconId = Microsoft.UI.Win32Interop.GetIconIdFromIcon(bmp.GetHicon());
                AppWindow.SetIcon(iconId);
            }
        } catch { }

        this.InitializeComponent();

        // Cloak the window BEFORE Activation or sizing to mask the compositor initialization
        var h = WindowNative.GetWindowHandle(this);
        
        LoadAndPosition(); // Call this BEFORE activating, so _dimWin doesn't steal focus later

        ApplyWindowFlags();
        this.Activated += OnActivated;
        this.Activate(); // Shows window (cloaked), stealing focus back correctly!

        // WinUI 3 resets DWM attributes on Activate(), so re-apply them IMMEDIATELY before compositor initializes
        var hw = WindowNative.GetWindowHandle(this);
        int p = _disableRoundedCorners ? 1 : DWMWCP_ROUND;
        DwmSetWindowAttribute(hw, DWMWA_WINDOW_CORNER_PREFERENCE, ref p, sizeof(int));
        
        int customBorderColor = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(hw, DWMWA_BORDER_COLOR, ref customBorderColor, sizeof(int));

        // Wait 100ms for the SystemBackdrop (Acrylic/Mica) to paint over the black frame
        _ = System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!_disableAnimation)
                {
                    _popIn.Begin();
                    
                    int steps = 15;
                    int startX = _animStartX;
                    int startY = _animStartY;
                    int finalX = _targetX;
                    int finalY = _targetY;
                    int currentStep = 0;
                    int lastX = startX;
                    int lastY = startY;

                    _ = System.Threading.Tasks.Task.Run(() => {
                        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

                        while (true) {
                            currentStep++;
                            double t = (double)currentStep / steps;
                            double ease = currentStep >= steps ? 1.0 : 1 - Math.Pow(2, -10 * t); // easeOutExpo

                            int currentX = (int)Math.Round(startX + (finalX - startX) * ease);
                            int currentY = (int)Math.Round(startY + (finalY - startY) * ease);

                            if (currentStep >= steps) {
                                currentX = finalX;
                                currentY = finalY;
                            }

                            if (currentX != lastX || currentY != lastY) {
                                SetWindowPos(hw, IntPtr.Zero, currentX, currentY, 0, 0,
                                    SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                                DwmFlush();
                                lastX = currentX;
                                lastY = currentY;
                            }
                            else {
                                DwmFlush();
                            }

                            if (currentStep >= steps) break;
                        }
                    });
                }
                else { _root.Opacity = 1; _rootScale.ScaleY = 1; _rootTranslate.Y = 0; }
            });
        });
    }

    void ApplyWindowFlags()
    {
        var h  = WindowNative.GetWindowHandle(this);
        int policy = _disableRoundedCorners ? 1 : DWMWCP_ROUND;
        DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref policy, sizeof(int));
        
        int customBorderColor = _popupIsDark ? 0x00363636 : 0x00D6D6D6;
        
        if (_overrideBorderColor && !string.IsNullOrEmpty(_customBorderColor) && _customBorderColor.Length >= 7)
        {
            byte r = byte.Parse(_customBorderColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(_customBorderColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(_customBorderColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            customBorderColor = b << 16 | g << 8 | r;
        }
        DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref customBorderColor, sizeof(int));
    }

    private IntPtr WindowSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == 0x0084) // WM_NCHITTEST
        {
            return (IntPtr)1; // HTCLIENT
        }
        if (uMsg == 0x0024) // WM_GETMINMAXINFO
        {
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            mmi.ptMinTrackSize.X = 10; // Allow super thin windows for column mode!
            mmi.ptMinTrackSize.Y = 10;
            Marshal.StructureToPtr(mmi, lParam, false);
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private async void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (sender is FrameworkElement el && el.DataContext is AppEntryViewModel vm)
        {
            var container = _compactR.ContainerFromItem(vm) as GridViewItem ?? _compactRVertical.ContainerFromItem(vm) as GridViewItem;
            if (container != null) Canvas.SetZIndex(container, 100);
        }
    }

    // ─── Icon loading ───────────────────────────────────────────────────────
    static async System.Threading.Tasks.Task<ImageSource?> LoadIconAsync(string exePath, string? cachedPng)
    {
        // Try cached PNG first
        if (!string.IsNullOrEmpty(cachedPng) && File.Exists(cachedPng))
            return new BitmapImage(new Uri(cachedPng));

        // Fallback: extract from EXE on thread-pool
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    ushort idx = 0;
                    IntPtr hIcon = ExtractAssociatedIcon(IntPtr.Zero, exePath, out idx);
                    if (hIcon == IntPtr.Zero) return null;

                    using var icon = System.Drawing.Icon.FromHandle(hIcon);
                    using var bmp  = icon.ToBitmap();
                    DestroyIcon(hIcon);

                    using var ms = new MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] bytes = ms.ToArray();

                    // Must marshal back to UI thread
                    return (ImageSource?) null; // return bytes as a flag
                }
                catch { return null; }
            });
        }
        return null;
    }

    // Synchronous icon extraction via System.Drawing (runs on STA thread)
    static string? ExtractIconSync(string exePath, string? cachedPng, bool monotone, bool onetone, Windows.UI.Color accColor)
    {
        if (!string.IsNullOrEmpty(cachedPng) && File.Exists(cachedPng))
        {
            if (!monotone && !onetone) return cachedPng;
        }

        string? sourceFile = null;
        if (!string.IsNullOrEmpty(cachedPng) && File.Exists(cachedPng))
        {
            sourceFile = cachedPng;
        }
        else if (!string.IsNullOrEmpty(exePath) && (File.Exists(exePath) || Directory.Exists(exePath)))
        {
            sourceFile = exePath;
        }
        else if (!string.IsNullOrEmpty(exePath))
        {
            var extractedFallback = TaskTile.Services.IconHelper.GetOrExtractIcon(exePath);
            if (!string.IsNullOrEmpty(extractedFallback) && (File.Exists(extractedFallback) || Directory.Exists(extractedFallback)))
                sourceFile = extractedFallback;
        }

        if (string.IsNullOrEmpty(sourceFile) || (!File.Exists(sourceFile) && !Directory.Exists(sourceFile))) return null;

        try
        {
            System.Drawing.Bitmap bmp;
            if (Directory.Exists(sourceFile))
            {
                bmp = TaskTile.Services.IconHelper.ExtractFolderIcon() ?? new System.Drawing.Bitmap(32, 32);
            }
            else if (sourceFile.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                sourceFile.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                sourceFile.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                sourceFile.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                bmp = new System.Drawing.Bitmap(sourceFile);
            }
            else
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(sourceFile);
                if (icon == null)
                {
                    string fallback = TaskTile.Services.IconHelper.GetOrExtractIcon(sourceFile);
                    if (!string.IsNullOrEmpty(fallback) && File.Exists(fallback))
                        bmp = new System.Drawing.Bitmap(fallback);
                    else
                        return null;
                }
                else
                {
                    bmp = icon.ToBitmap();
                }
            }

            if (monotone || onetone)
            {
                float r = onetone ? accColor.R / 255f : 1f;
                float g = onetone ? accColor.G / 255f : 1f;
                float b = onetone ? accColor.B / 255f : 1f;
                
                float[][] colorMatrixElements = { 
                   new float[] {0.33f * r,  0.33f * g,  0.33f * b,  0, 0},        
                   new float[] {0.33f * r,  0.33f * g,  0.33f * b,  0, 0},        
                   new float[] {0.33f * r,  0.33f * g,  0.33f * b,  0, 0},        
                   new float[] {0,          0,          0,          1, 0},        
                   new float[] {0,          0,          0,          0, 1}
                };
                var colorMatrix = new System.Drawing.Imaging.ColorMatrix(colorMatrixElements);
                var imageAttributes = new System.Drawing.Imaging.ImageAttributes();
                imageAttributes.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);

                var monoBmp = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
                using (var graphics = System.Drawing.Graphics.FromImage(monoBmp))
                {
                    graphics.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, imageAttributes);
                }
                bmp.Dispose();
                bmp = monoBmp;
            }

            using var ms  = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            bmp.Dispose();
            ms.Seek(0, SeekOrigin.Begin);
            
            string suffix = onetone ? "_onetone" : (monotone ? "_mono" : "");
            string safeName = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(exePath) ? sourceFile : exePath);
            string tmp = Path.Combine(Path.GetTempPath(), $"ti_{safeName}{suffix}.png");
            File.WriteAllBytes(tmp, ms.ToArray());
            return tmp;
        }
        catch { return null; }
    }

    // ─── Event Handlers for XAML ─────────────────────────────────────────────

    private long _lastWheelTicks = 0;
    public void ClassicSV_PointerWheelChanged(object sender, PointerRoutedEventArgs pArgs)
    {
        pArgs.Handled = true;
        if (_classicPages.Count <= 1) return;

        long now = Environment.TickCount64;
        if (now - _lastWheelTicks < 200) return;

        var delta = pArgs.GetCurrentPoint(null).Properties.MouseWheelDelta;
        if (delta < 0 && _classicPage < _classicPages.Count - 1)
        {
            _lastWheelTicks = now;
            GoToPage(_classicPage + 1, +1);
        }
        else if (delta > 0 && _classicPage > 0)
        {
            _lastWheelTicks = now;
            GoToPage(_classicPage - 1, -1);
        }
    }

    // ─── Start Menu hover effect ─────────────────────────────────────────────
    // Replicates the Win11 Start Menu folder tile: delayed grow + gradient border on enter,
    // quick shrink + dark flash + fade out on exit.
    static readonly LinearGradientBrush _elevationBorder = new LinearGradientBrush
    {
        StartPoint = new Windows.Foundation.Point(0.5, 0),
        EndPoint   = new Windows.Foundation.Point(0.5, 1),
        GradientStops =
        {
            new GradientStop { Color = Windows.UI.Color.FromArgb(90, 255, 255, 255), Offset = 0 },
            new GradientStop { Color = Windows.UI.Color.FromArgb(20, 255, 255, 255), Offset = 1 },
        }
    };

    // ─── Load data + position ───────────────────────────────────────────────
    public void LoadGroup(string groupId)
    {
        _groupId = groupId;
        LoadAndPosition();
        
        // RESET PAGE
        _classicPage = 0;
        _classicSV.ChangeView(null, 0, null, true); // true = disable animation
        UpdateClassicPagingVisuals();

        int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;
        int borderTop = (AppWindow.Size.Height - AppWindow.ClientSize.Height) / 2;

        var hwnd = WindowNative.GetWindowHandle(this);
        SetWindowPos(hwnd, IntPtr.Zero, _animStartX - borderX, _animStartY - borderTop, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        
        if (!_disableAnimation)
        {
            _popIn.Begin();
            
            int steps = 15;
            int startX = _animStartX - borderX;
            int startY = _animStartY - borderTop;
            int finalX = _targetX - borderX;
            int finalY = _targetY - borderTop;
            int currentStep = 0;
            int lastX = startX;
            int lastY = startY;

            System.Threading.Tasks.Task.Run(() => {
                System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

                while (true) {
                    currentStep++;
                    double t = (double)currentStep / steps;
                    double ease = 1 - Math.Pow(1 - t, 3); // easeOutCubic

                    int currentX = (int)Math.Round(startX + (finalX - startX) * ease);
                    int currentY = (int)Math.Round(startY + (finalY - startY) * ease);

                    if (currentStep >= steps) {
                        currentX = finalX;
                        currentY = finalY;
                    }

                    if (currentX != lastX || currentY != lastY) {
                        SetWindowPos(hwnd, IntPtr.Zero, currentX, currentY, 0, 0,
                            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                        TaskTile.NativeMethods.DwmFlush();
                        lastX = currentX;
                        lastY = currentY;
                    }
                    else {
                        TaskTile.NativeMethods.DwmFlush();
                    }

                    if (currentStep >= steps) break;
                }
            });
        }
    }

    void LoadAndPosition()
    {
        var apps = new ObservableCollection<AppEntryViewModel>();
        string name="Group"; bool hideName=false, hideAppLabels=false, showCardLabels=false; int popupStyle=0,compactAlign=0,gridCols=3,gridRows=0,themeOverride=0,appIconStyle=0;
        bool launchAtCenter=false, makeMainFocus=false, overrideLaunchSide=false; int groupLaunchSide=0;
        bool disableAnimation = false, disableAutoHide = false, disableFloat = false, disableRoundedCorners = false, keepOpen = false;
        int groupTitleAlign = -1;

        Brush accentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Windows.UI.Color accColor = Windows.UI.Color.FromArgb(255, 0, 120, 215);
        if (Application.Current.Resources.TryGetValue("SystemControlHighlightAccentBrush", out var res) && res is Brush ab)
        {
            accentBrush = ab;
            if (ab is SolidColorBrush sb) accColor = sb.Color;
        }

        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile","groups.json");
            if (File.Exists(file))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var g in doc.RootElement.EnumerateArray())
                {
                    if (string.IsNullOrEmpty(_groupId) || g.GetProperty("Id").GetString() != _groupId) continue;
                    name = g.GetProperty("Name").GetString()!;
                    if (g.TryGetProperty("HideName",          out var p)) hideName       = p.GetBoolean();
                    if (g.TryGetProperty("HideAppLabels",     out p))    hideAppLabels  = p.GetBoolean();
                    if (g.TryGetProperty("ShowCardLabels",    out p))    showCardLabels = p.GetBoolean();
                    bool marqueeAppLabels = false;
                    bool scrollAppLabels = false;
                    if (g.TryGetProperty("MarqueeAppLabels",  out p))    marqueeAppLabels = p.GetBoolean();
                    if (g.TryGetProperty("ScrollAppLabels",   out p))    scrollAppLabels  = p.GetBoolean();
                    _marqueeAppLabels = marqueeAppLabels;
                    _scrollAppLabels  = scrollAppLabels;
                    if (g.TryGetProperty("PopupStyle",         out p))    popupStyle      = p.GetInt32();
                    if (g.TryGetProperty("BackdropStyle",      out p))    _backdropStyle   = p.GetInt32();
                    if (g.TryGetProperty("CompactAlignment",   out p))    compactAlign    = p.GetInt32();
                    
                    // Legacy fallback
                    if (g.TryGetProperty("MonotoneIcon",       out p) && p.GetBoolean()) appIconStyle = 1;

                    if (g.TryGetProperty("AppIconStyle",       out p))    appIconStyle    = p.GetInt32();
                    if (g.TryGetProperty("ThemeOverride",      out p))    themeOverride   = p.GetInt32();
                    if (g.TryGetProperty("GridColumns",        out p))    gridCols        = p.GetInt32();
                    if (g.TryGetProperty("GridRows",           out p))    gridRows        = p.GetInt32();
                    if (g.TryGetProperty("LaunchAtCenter",     out p))    launchAtCenter  = p.GetBoolean();
                    if (g.TryGetProperty("MakeMainFocus",      out p))    makeMainFocus   = p.GetBoolean();
                    if (g.TryGetProperty("OverrideLaunchSide", out p))    overrideLaunchSide = p.GetBoolean();
                    if (g.TryGetProperty("OverrideBorderColor", out p))   _overrideBorderColor = p.GetBoolean();
                    if (g.TryGetProperty("CustomBorderColor",  out p))    _customBorderColor = p.GetString() ?? "";
                    if (g.TryGetProperty("GroupLaunchSide",    out p))    groupLaunchSide = p.GetInt32();
                                        if (g.TryGetProperty("IsDesktopMode",      out p))    _isDesktopMode  = p.GetBoolean();
                    if (g.TryGetProperty("DisableAnimation",   out p))    disableAnimation = p.GetBoolean();
                    if (g.TryGetProperty("DisableAutoHide",    out p))    disableAutoHide = p.GetBoolean();
                    if (g.TryGetProperty("DisableFloat",       out p))    disableFloat = p.GetBoolean();
                    if (g.TryGetProperty("DisableRoundedCorners", out p)) disableRoundedCorners = p.GetBoolean();
                    if (g.TryGetProperty("KeepOpen", out p)) keepOpen = p.GetBoolean();
                    if (g.TryGetProperty("TitleAlignment", out p)) groupTitleAlign = p.GetInt32();

                    bool isDynamicFolder = false;
                    string dynamicFolderPath = string.Empty;
                    if (g.TryGetProperty("IsDynamicFolder", out p)) isDynamicFolder = p.GetBoolean();
                    if (g.TryGetProperty("DynamicFolderPath", out p)) dynamicFolderPath = p.GetString() ?? "";

                    bool uwp = appIconStyle == 1;
                    bool monotone = appIconStyle == 2;
                    bool onetone = appIconStyle == 3;

                    if (isDynamicFolder && !string.IsNullOrEmpty(dynamicFolderPath) && System.IO.Directory.Exists(dynamicFolderPath))
                    {
                        var files = System.IO.Directory.GetFiles(dynamicFolderPath);
                        foreach (var f in files)
                        {
                            var info = new System.IO.FileInfo(f);
                            if ((info.Attributes & System.IO.FileAttributes.Hidden) != 0 || (info.Attributes & System.IO.FileAttributes.System) != 0)
                                continue;
                                
                            ImageSource? initialIcon = null;
                            if (!monotone && !onetone && !string.IsNullOrEmpty(f) && File.Exists(f) && (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)))
                            {
                                try { initialIcon = new BitmapImage(new Uri(f)); } catch { }
                            }

                            var entry = new AppEntryViewModel
                            {
                                Name               = System.IO.Path.GetFileName(f),
                                ExePath            = f,
                                IconImage          = initialIcon,
                                NormalVisibility   = monotone ? Visibility.Collapsed : Visibility.Visible,
                                MonotoneVisibility = monotone ? Visibility.Visible   : Visibility.Collapsed,
                                LabelVisibility    = hideAppLabels ? Visibility.Collapsed : Visibility.Visible,
                                UwpBackground      = uwp ? accentBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                                TileCornerRadius   = uwp ? new CornerRadius(0) : new CornerRadius(10)
                            };
                            apps.Add(entry);

                            if (initialIcon == null)
                            {
                                var dispatcher = this.DispatcherQueue;
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    var extracted = ExtractIconSync(f, "", monotone, onetone, accColor);
                                    if (extracted != null)
                                    {
                                        dispatcher?.TryEnqueue(() => entry.IconImage = new BitmapImage(new Uri(extracted)));
                                    }
                                });
                            }
                        }
                    }
                    else if (g.TryGetProperty("Apps", out var arr))
                    {
                        foreach (var a in arr.EnumerateArray())
                        {
                            var exe  = a.GetProperty("ExePath").GetString()!;
                            var icon = a.TryGetProperty("IconPath", out var ic) ? ic.GetString() : "";

                            ImageSource? initialIcon = null;
                            if (!monotone && !onetone && !string.IsNullOrEmpty(icon) && File.Exists(icon))
                            {
                                try { initialIcon = new BitmapImage(new Uri(icon)); } catch { }
                            }
                            else if (!monotone && !onetone && !string.IsNullOrEmpty(exe) && File.Exists(exe) && (exe.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || exe.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)))
                            {
                                try { initialIcon = new BitmapImage(new Uri(exe)); } catch { }
                            }

                            var entry = new AppEntryViewModel
                            {
                                Name               = a.GetProperty("Name").GetString()!,
                                ExePath            = exe,
                                IconImage          = initialIcon,
                                NormalVisibility   = monotone ? Visibility.Collapsed : Visibility.Visible,
                                MonotoneVisibility = monotone ? Visibility.Visible   : Visibility.Collapsed,
                                LabelVisibility    = hideAppLabels ? Visibility.Collapsed : Visibility.Visible,
                                LabelTrimming      = (marqueeAppLabels || scrollAppLabels) ? TextTrimming.None : TextTrimming.CharacterEllipsis,
                                UwpBackground      = uwp ? accentBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                                TileCornerRadius   = uwp ? new CornerRadius(0) : new CornerRadius(10)
                            };
                            apps.Add(entry);

                            if (initialIcon == null)
                            {
                                var dispatcher = this.DispatcherQueue;
                                _ = System.Threading.Tasks.Task.Run(() =>
                                {
                                    var extracted = ExtractIconSync(exe, icon, monotone, onetone, accColor);
                                    if (extracted != null)
                                    {
                                        dispatcher?.TryEnqueue(() => entry.IconImage = new BitmapImage(new Uri(extracted)));
                                    }
                                });
                            }
                        }
                    }
                    break;
                }
            }
        }
        catch { }

        // Theme / backdrop
        var s = TaskTile.Services.SettingsService.Current;
        
        var finalLaunchSide = overrideLaunchSide ? (TaskTile.Models.LaunchSide)(groupLaunchSide + 1) : s.LaunchSide;
        launchAtCenter = (finalLaunchSide == TaskTile.Models.LaunchSide.Center);
        makeMainFocus = makeMainFocus || s.GlobalMakeMainFocus;
        disableAnimation = disableAnimation || s.DisableAnimation;
        _disableAnimation = disableAnimation;
        disableAutoHide = disableAutoHide || s.DisableAutoHide;
          disableFloat = disableFloat || s.DisableFloat;
          disableRoundedCorners = disableRoundedCorners || s.DisableRoundedCorners;
        // keepOpen only exists on group
          _keepOpen = keepOpen;
        _disableAutoHide = disableAutoHide;
        _disableRoundedCorners = disableRoundedCorners;
        _disableFloat = disableFloat;

        _makeMainFocus = makeMainFocus && launchAtCenter;

        if (themeOverride == 0 && s.ApplyGlobalConfigToPopups)
        {
            themeOverride = s.Theme;
        }

        if (Content is FrameworkElement fe)
        {
            if (themeOverride == 1) fe.RequestedTheme = ElementTheme.Light;
            else if (themeOverride == 2) fe.RequestedTheme = ElementTheme.Dark;
            else fe.RequestedTheme = ElementTheme.Default;

            bool isSystemDark = false; try { using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) { if (key != null && key.GetValue("AppsUseLightTheme") is int val) { isSystemDark = val == 0; } } } catch {} bool popupIsDark = themeOverride == 2 || (themeOverride == 0 && isSystemDark);
        _popupIsDark = popupIsDark; // dark unless light override
            
            // Fix: Actually apply the requested theme to the window root
            _root.RequestedTheme = popupIsDark ? ElementTheme.Dark : ElementTheme.Light; int darkAttr = popupIsDark ? 1 : 0; DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), 20, ref darkAttr, sizeof(int));
            // DWMWA_COLOR_NONE: no visible border ring. Applied here AND re-applied after
            // Activate() in the uncloak callback, because WinUI 3 resets DWM attributes on Activate.
            // DWMWA_COLOR_NONE removed to restore native DWM border
            
            if (fe != null) fe.RequestedTheme = popupIsDark ? ElementTheme.Dark : ElementTheme.Light;

        }
        
        bool forceDark = _popupIsDark;
        var hwnd = WindowNative.GetWindowHandle(this);

        MARGINS margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        if (_backdropStyle == -1 || _backdropStyle == 3)
        {
            SystemBackdrop = null;
            _root.Background = new SolidColorBrush(forceDark ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White); // OLED black background or solid white
            int dwmBackdrop = 1; // DWMSBT_NONE
            DwmSetWindowAttribute(hwnd, 38, ref dwmBackdrop, sizeof(int));
        }
        else
        {
            _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            if (_backdropStyle == 1)
            {
                SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop { Kind = MicaKind.Base };
                int dwmBackdrop = 2; // DWMSBT_MAINWINDOW (Mica)
                DwmSetWindowAttribute(hwnd, 38, ref dwmBackdrop, sizeof(int));
            }
            else if (_backdropStyle == 2)
            {
                SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop { Kind = MicaKind.BaseAlt };
                int dwmBackdrop = 4; // DWMSBT_TABBEDWINDOW (Mica Alt)
                DwmSetWindowAttribute(hwnd, 38, ref dwmBackdrop, sizeof(int));
            }
            else
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
                int dwmBackdrop = 3; // DWMSBT_TRANSIENTWINDOW (Acrylic)
                DwmSetWindowAttribute(hwnd, 38, ref dwmBackdrop, sizeof(int));
            }
        }

        int winBorderColor = _overrideBorderColor && !string.IsNullOrEmpty(_customBorderColor) && _customBorderColor.Length >= 7
            ? (byte.Parse(_customBorderColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber) << 16 |
               byte.Parse(_customBorderColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber) << 8 |
               byte.Parse(_customBorderColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber))
            : (forceDark ? 0x00363636 : 0x00D6D6D6);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref winBorderColor, sizeof(int));

        // Title
        bool noTitle = hideName || popupStyle == 1;
        _title.Visibility = noTitle ? Visibility.Collapsed : Visibility.Visible;
        if (!noTitle) _title.Text = name;

        int alignPref = groupTitleAlign != -1 ? groupTitleAlign : s.TitleAlignment;
        HorizontalAlignment titleAlign = HorizontalAlignment.Center;
        if (alignPref == 0) titleAlign = HorizontalAlignment.Left;
        else if (alignPref == 2) titleAlign = HorizontalAlignment.Right;

        int n = apps.Count;
        if (n == 0) return;

        double logW = 0, logH = 0;

        if (popupStyle == 0) // Classic
        {
            _border.Padding = new Thickness(12);
            _classicSV.Visibility  = Visibility.Visible;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;

            int requestedCols = gridCols > 0 ? gridCols : 3;
            int cols = requestedCols;
            if (cols == 0) cols = 3;
            int rowMax  = gridRows > 0 ? gridRows : 3;
            int pageSize = cols * rowMax;

            int cell = 84, sp = 0;
            
            int visibleRows = (n <= pageSize) ? (int)Math.Ceiling((double)n / cols) : rowMax;
            if (visibleRows == 0) visibleRows = 1;
            int gH = visibleRows * cell + (visibleRows - 1) * sp;

            _classicPages.Clear();
            _classicPage = 0;

            if (n <= pageSize)
            {
                _classicR.Width = cols * cell + 16;
                _classicR.Height = gH + 8;
                _classicR.ItemsSource = apps;
                _classicR.Visibility  = Visibility.Visible;
                _pageDots.Visibility  = Visibility.Collapsed;
                _classicPagesPanel.Children.Clear();
                _classicPagesPanel.Children.Add(_classicR);
            }
            else
            {
                _classicR.Visibility = Visibility.Collapsed;
                _classicPagesPanel.Children.Clear();
                for (int i = 0; i < n; i += pageSize)
                {
                    var page = apps.Skip(i).Take(pageSize).ToList();
                    _classicPages.Add(page);
                    var gv = new GridView {
                        ItemsSource = page,
                        Width = cols * cell + 16,
                        Height = gH + 8,
                        Margin = new Thickness(0, 0, 0, 20),
                        Padding = new Thickness(8, 4, 8, 4),
                        ItemTemplate = _classicR.ItemTemplate,
                        ItemContainerStyle = _classicR.ItemContainerStyle,
                        SelectionMode = ListViewSelectionMode.None,
                        IsItemClickEnabled = true,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    if (!_disableAnimation)
                    {
                        var trans = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
                        trans.Add(new Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition { IsStaggeringEnabled = true });
                        gv.ItemContainerTransitions = trans;
                    }

                    gv.ItemClick += App_ItemClick;
                    _classicPagesPanel.Children.Add(gv);
                }

                UpdateClassicPagingUI();
                _pageDots.Visibility = Visibility.Visible;
            }

            _classicSV.Margin = new Thickness(0);
            _classicR.Padding = new Thickness(8, 4, 8, 4); 
            _classicSV.Height = gH + 8;
            _classicSV.MaxHeight = double.PositiveInfinity;

            _content.Padding = new Thickness(12, 12, 12, 4);
            _title.HorizontalAlignment = titleAlign;
            _title.Margin = new Thickness(0, 0, 0, 16);
            _holder.HorizontalAlignment = HorizontalAlignment.Center;
            _holder.VerticalAlignment = VerticalAlignment.Center;
            
            logW = 24 + (cols * cell + 16) + 24;
            logH = (noTitle ? 0 : 38) + (gH + 8) + 24 + 16;
        }
        else if (popupStyle == 1) // Compact
        {
            _pageDots.Visibility  = Visibility.Collapsed;
            _classicSV.Visibility = Visibility.Collapsed;
            _modernSV.Visibility  = Visibility.Collapsed;
            _listSV.Visibility    = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;
            _compactR.Visibility  = Visibility.Collapsed;
            _compactRVertical.Visibility = Visibility.Collapsed;

            if (compactAlign == 0) 
            { 
                _compactR.Visibility  = Visibility.Visible;
                _compactR.ItemsSource = apps;
                _compactR.Width = n * 42;
                logW = n * 42 + 12;
                logH = 30;
                _compactR.HorizontalAlignment = HorizontalAlignment.Center;
                _compactR.VerticalAlignment = VerticalAlignment.Center;
                _holder.HorizontalAlignment   = HorizontalAlignment.Center;
                _holder.VerticalAlignment     = VerticalAlignment.Center;
                _compactR.Height = 26;
                _compactR.Padding = new Thickness(0);
                _compactR.Margin = new Thickness(0);
                _compactR.BorderThickness = new Thickness(0);
                _border.Padding = new Thickness(6, 2, 6, 2);
            }
            else                   
            { 
                _compactRVertical.Visibility = Visibility.Visible;
                _compactRVertical.ItemsSource = apps;
                _compactRVertical.Height = n * 42;
                logH = n * 42 + 12;  
                logW = 30;     
                _compactRVertical.HorizontalAlignment = HorizontalAlignment.Center;
                _compactRVertical.VerticalAlignment = VerticalAlignment.Center;
                _holder.HorizontalAlignment   = HorizontalAlignment.Center;
                _holder.VerticalAlignment     = VerticalAlignment.Center;
                _compactRVertical.Width = 26;
                _compactRVertical.Padding = new Thickness(0);
                _compactRVertical.Margin = new Thickness(0);
                _compactRVertical.BorderThickness = new Thickness(0);
                _border.Padding = new Thickness(2, 6, 2, 6);
            }

            _content.Padding = new Thickness(0);
            _title.Margin = new Thickness(0);
        }
        else if (popupStyle == 2) // Modern
        {
            _border.Padding = new Thickness(0);
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Visible;
            _pageDots.Visibility   = Visibility.Collapsed;
            _modernR.ItemsSource   = apps;

            int requestedCols = gridCols > 0 ? gridCols : 3;
            int cols = Math.Min(n, requestedCols);
            if (cols == 0) cols = 1;
            int allRows = (int)Math.Ceiling((double)n / cols);
            
            _modernR.Width = cols * 68 + 24;
            _modernR.Height = allRows * 68 + 24;
            _modernR.Margin = new Thickness(0);
            _modernR.HorizontalAlignment = HorizontalAlignment.Center;
            _modernR.VerticalAlignment = VerticalAlignment.Center;
            _holder.HorizontalAlignment = HorizontalAlignment.Center;
            _holder.VerticalAlignment = VerticalAlignment.Center;

            _title.HorizontalAlignment = titleAlign;
            _title.Margin = titleAlign == HorizontalAlignment.Left ? new Thickness(4, 8, 0, 14) : 
                            titleAlign == HorizontalAlignment.Right ? new Thickness(0, 8, 4, 14) : 
                            new Thickness(0, 8, 0, 14);

            _content.Padding = new Thickness(16, 16, 16, 12);
            
            logW = 32 + (int)_modernR.Width;
            int titleH = noTitle ? 0 : (22 + 22);
            int overhead = 28 + titleH;
            logH = overhead + (int)_modernR.Height;
            
            _modernSV.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }
        else if (popupStyle == 3) // List
        {
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;
            _pageDots.Visibility   = Visibility.Collapsed;
            
            _listSV.Visibility     = Visibility.Visible;
            _listR.ItemsSource     = apps;
            _listR.Padding         = new Thickness(0);
            _listR.Margin          = new Thickness(0);
            
            _cardFooterName.Text = name;
            _cardFooterName.Margin = new Thickness(14, 0, 0, 0);
            
            var barBorder = _listR.Parent as Grid;
            if (barBorder != null) {
                var barBorderChild = barBorder.Children.Count > 1 ? barBorder.Children[1] as Border : null;
                if (barBorderChild != null) {
                    barBorderChild.Background = new SolidColorBrush(forceDark ? Windows.UI.Color.FromArgb(0, 0, 0, 0) : Windows.UI.Color.FromArgb(64, 0, 0, 0));
                }
            }

            _holder.HorizontalAlignment = HorizontalAlignment.Stretch;
            _holder.VerticalAlignment   = VerticalAlignment.Top;

            _border.Padding  = new Thickness(14, 12, 14, 6); 
            _content.Padding = new Thickness(0, noTitle ? 2 : 0, 0, 0); 
            
            _title.HorizontalAlignment = titleAlign;
            _title.Margin = titleAlign == HorizontalAlignment.Left ? new Thickness(16, 4, 0, 10) : 
                            titleAlign == HorizontalAlignment.Right ? new Thickness(0, 4, 16, 10) : 
                            new Thickness(0, 4, 0, 10);

            logW = 260;

            int overhead = noTitle ? 8 : (24 + 14 + 14); // 52px total overhead gives title comfortable breathing room
            logH = overhead + n * 36;
        }

        else // Dialog-ish
        {
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _pageDots.Visibility   = Visibility.Collapsed;
            _title.Visibility      = Visibility.Collapsed; // Title lives in the bottom bar

            _cardContainer.Visibility = Visibility.Visible;
            _cardFooterName.Text      = name;
            _cardFooterName.HorizontalAlignment = HorizontalAlignment.Center;
            _cardFooterName.Margin    = new Thickness(0);

            // Edge-to-edge flush border, no nested side gaps
            _border.Padding = new Thickness(0);
            _content.Padding = new Thickness(0);
            _content.Margin = new Thickness(0);
            _holder.Margin = new Thickness(0);
            _holder.HorizontalAlignment = HorizontalAlignment.Stretch;
            _holder.VerticalAlignment   = VerticalAlignment.Stretch;

            // Set bottom bar background and border colors
            _cardFooterBorder.VerticalAlignment = VerticalAlignment.Stretch;
            _cardFooterBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            _cardFooterBorder.MinHeight = 40;
            _cardFooterBorder.Padding = new Thickness(8, 8, 8, 8);
            _cardFooterBorder.BorderThickness = new Thickness(0, 1, 0, 0);
            _cardFooterBorder.BorderBrush = new SolidColorBrush(forceDark 
                ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF) 
                : Windows.UI.Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            _cardFooterBorder.Background = new SolidColorBrush(forceDark 
                ? Windows.UI.Color.FromArgb(0x25, 0x00, 0x00, 0x00) 
                : Windows.UI.Color.FromArgb(0x35, 0x00, 0x00, 0x00));

            // Always icon-only — no labels in Dialog-ish
            foreach (var app in apps)
                app.LabelVisibility = Visibility.Collapsed;

            _cardR.ItemsSource = apps;

            int requestedCols = gridCols > 0 ? gridCols : 3;
            int cols = Math.Min(n, requestedCols);
            if (cols == 0) cols = 3;
            int allRows = (int)Math.Ceiling((double)n / cols);

            int cell = 52;
            int gridW = cols * cell + 16;
            int gridH = allRows * cell + 8;
            
            _cardR.Width = gridW;
            _cardR.Height = gridH;
            _cardR.Padding = new Thickness(8, 4, 8, 4);
            _cardR.HorizontalAlignment = HorizontalAlignment.Center;
            
            logW = gridW;
            logH = gridH + 44; // A tiny bit taller with comfortable chin and grid!
        }



        // "Make main focus" dim overlay (covers entire screen behind popup)
        if (_makeMainFocus)
            ShowDimOverlay();

        // Position
        GetCursorPos(out var pt);
        hwnd  = WindowNative.GetWindowHandle(this);
        var winId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var da    = DisplayArea.GetFromWindowId(winId, DisplayAreaFallback.Primary);

        uint dpi   = GetDpiForWindow(hwnd);
        float sc   = dpi == 0 ? 1f : dpi / 96f;

        // Modern uses internal auto sizing and the user explicitly requested it grow indefinitely without scrolling.

        double scale = _root.XamlRoot?.RasterizationScale ?? 1.0;
        int physW = (int)Math.Round(logW * scale);
        int physH = (int)Math.Round(logH * scale);

        if (disableRoundedCorners) { _border.CornerRadius = new CornerRadius(0); _border.BorderThickness = new Thickness(0); }
        if (disableAnimation) {
            _modernR.ItemContainerTransitions.Clear();
            _classicR.ItemContainerTransitions.Clear();
            _compactR.ItemContainerTransitions.Clear();
            _listR.ItemContainerTransitions.Clear();
            _cardR.ItemContainerTransitions.Clear();
        }
          _physW = physW;
        _physH_original = physH;

        var work = da.WorkArea;
        var outer = da.OuterBounds;
        physH = Math.Min(physH, work.Height - 64);

        int x, y;
        bool isTop = false, isLeft = false, isRight = false;
        if (launchAtCenter && popupStyle != 1 && popupStyle != 3) // Disallow for List/Compact
        {
            x = work.X + work.Width  / 2 - physW / 2;
            y = work.Y + work.Height / 2 - physH / 2;
        }
        else if (_isDesktopMode)
        {
            // Offset gracefully from the cursor to not cover the clicked icon
            x = pt.X + 16;
            y = pt.Y + 16;
            
            // Push it left/up if too close to right/bottom edges
            if (x + physW > work.X + work.Width)  x = pt.X - physW - 8;
            if (y + physH > work.Y + work.Height) y = pt.Y - physH - 8;

            x = Math.Clamp(x, work.X, work.X + work.Width  - physW);
            y = Math.Clamp(y, work.Y, work.Y + work.Height - physH);
        }
        else
        {
            // Respect explicit LaunchSide from settings
            var launchSide = finalLaunchSide;
            if (launchSide == TaskTile.Models.LaunchSide.Top)         { isTop = true;  }
            else if (launchSide == TaskTile.Models.LaunchSide.Left)   { isLeft = true; }
            else if (launchSide == TaskTile.Models.LaunchSide.Right)  { isRight = true; }
            else if (launchSide == TaskTile.Models.LaunchSide.Bottom) { }
            else // Auto / Fallback
            {
                isTop   = work.Y > outer.Y;
                isLeft  = work.X > outer.X;
                isRight = work.Width < outer.Width && work.X == outer.X;
            }

            const int gap = 12;
            APPBARDATA abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
            nint state = SHAppBarMessage(ABM_GETSTATE, ref abd);
            bool isAutoHidden = (state.ToInt32() & ABS_AUTOHIDE) != 0;
            int autoHideBuffer = isAutoHidden ? 48 : 0;

            if      (isLeft)  { x = work.X + gap;                                              y = pt.Y - physH / 2; }
            else if (isRight) { x = work.X + work.Width - physW - gap;                         y = pt.Y - physH / 2; }
            else if (isTop)   { y = work.Y + gap;                                              x = pt.X - physW / 2; }
            else              { y = work.Y + work.Height - physH - gap - autoHideBuffer;       x = pt.X - physW / 2; }

            if (_disableFloat) {
                if (isLeft) x = work.X;
                else if (isRight) x = work.X + work.Width - physW;
                else if (isTop) y = work.Y;
                else y = work.Y + work.Height - physH - autoHideBuffer;
                
                if (!_disableRoundedCorners) {
                    _border.CornerRadius = isTop ? new CornerRadius(0, 0, 4, 4) : isLeft ? new CornerRadius(0, 4, 4, 0) : isRight ? new CornerRadius(4, 0, 0, 4) : new CornerRadius(4, 4, 0, 0);
                    _border.BorderThickness = new Thickness(0);
                }
            }

            x = Math.Clamp(x, work.X, work.X + work.Width  - physW);
            y = Math.Clamp(y, work.Y, work.Y + work.Height - physH - autoHideBuffer);

            // Apply launch-side-aware pop-in scale animation
            if (!_isDesktopMode && _popIn != null)
            {
                if (!_disableAnimation)
                {
                    _root.Opacity = 0;
                }
            }
        }

        // Calculate invisible borders to offset the Move, ensuring the visible content is perfectly positioned
        int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;
        int borderTop = (AppWindow.Size.Height - AppWindow.ClientSize.Height) / 2; // Assuming symmetric vertical invisible borders

        // Set up animation positions and move to start immediately
        _targetX = x;
        _targetY = y;
        _animStartX = x;
        _animStartY = y;
        int animDistance = 40;
        if (!_disableAnimation)
        {
            if (isTop) _animStartY -= animDistance;
            else if (isLeft) _animStartX -= animDistance;
            else if (isRight) _animStartX += animDistance;
            else _animStartY += animDistance; // bottom default
        }
        
        // Clamp _animStartY to prevent the window from being positioned at Y < 0, which breaks DWM Acrylic
        if (_animStartY - borderTop < 0) _animStartY = borderTop;

        // Use ResizeClient to guarantee exact client area, preventing right-edge clipping.
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(physW, physH));
        
        AppWindow.Move(new Windows.Graphics.PointInt32(_animStartX - borderX, _animStartY - borderTop));

        // Removed SetWindowRgn to prevent jagged corners!

        // Live taskbar binding (Auto-hide sliding animation sync)
        if (!_isDesktopMode && !disableAutoHide && !makeMainFocus)
        {
            var trayHwnd = FindWindow("Shell_TrayWnd", null);
            GetWindowRect(trayHwnd, out _lastTrayRect);

            _taskbarTracker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _taskbarTracker.Tick += (s, e) =>
            {
                if (GetWindowRect(trayHwnd, out var currentTrayRect))
                {
                    int dx = currentTrayRect.Left - _lastTrayRect.Left;
                    int dy = currentTrayRect.Top - _lastTrayRect.Top;
                    if (dx != 0 || dy != 0)
                    {
                        GetWindowRect(hwnd, out var myRect);
                        SetWindowPos(hwnd, HWND_TOPMOST, myRect.Left + dx, myRect.Top + dy, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
                        _lastTrayRect = currentTrayRect;
                    }
                }
            };
            if (!_disableAutoHide) _taskbarTracker.Start();
        }

        if (_disableAnimation) 
        { 
            if (this.Content is FrameworkElement root) root.Transitions?.Clear(); 
            _classicR.ItemContainerTransitions?.Clear();
            _modernR.ItemContainerTransitions?.Clear();
            _compactR.ItemContainerTransitions?.Clear();
            _compactRVertical.ItemContainerTransitions?.Clear();
            _listR.ItemContainerTransitions?.Clear();
        }
    }

    Window? _dimWin;
    bool _isClosing = false;

    // ─── "Make main focus" — full-screen dim/blur overlay ──────────────────
    void ShowDimOverlay()
    {
        _dimWin = new Window();
        try { _dimWin.ExtendsContentIntoTitleBar = false; } catch { }
        
        _dimWin.SystemBackdrop = new StartMenuAcrylicBackdrop(true, 0.25f, 0.60f);

        // Darkens the blurred background slightly more
        var dimGrid = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)), IsHitTestVisible = true };
        dimGrid.PointerPressed += async (_, _) => { await CloseWithFadeAsync(); };
        _dimWin.Content = dimGrid;

        var dimHwnd = WindowNative.GetWindowHandle(_dimWin);
        int ex = GetWindowLong(dimHwnd, GWL_EXSTYLE);
        SetWindowLong(dimHwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);

        var hwnd = WindowNative.GetWindowHandle(this);
        var da = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd), DisplayAreaFallback.Primary);
        
        // Move dim window to the matching display then full-screen it natively
        _dimWin.AppWindow.Move(new Windows.Graphics.PointInt32(da.WorkArea.X, da.WorkArea.Y));
        _dimWin.AppWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);

        _dimWin.Activate();

        // Fade in color over backdrop
        var sbDim = new Storyboard();
        var opDim = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(200)) };
        Storyboard.SetTarget(opDim, dimGrid);
        Storyboard.SetTargetProperty(opDim, "Opacity");
        sbDim.Children.Add(opDim);
        sbDim.Begin();

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        this.Closed += (_, _) => { try { _dimWin?.Close(); } catch { } };
    }

    async System.Threading.Tasks.Task CloseWithFadeAsync()
    {
        if (_isClosing) return;
        _isClosing = true;

        if (_taskbarTracker != null)
        {
            _taskbarTracker.Stop();
        }

        var sbPopup = new Storyboard();
        if (_dimWin?.Content is Grid dg)
        {
            var daDim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(150)) };
            Storyboard.SetTarget(daDim, dg);
            Storyboard.SetTargetProperty(daDim, "Opacity");
            sbPopup.Children.Add(daDim);
        }
        sbPopup.Begin();

        if (!_disableAnimation)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            GetWindowRect(hwnd, out var currentRect);
            
            int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;
            int borderTop = (AppWindow.Size.Height - AppWindow.ClientSize.Height) / 2;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int startX = currentRect.Left;
            int startY = currentRect.Top;
            int steps = 10;
            int finalX = _animStartX - borderX;
            int finalY = _animStartY - borderTop;
            int currentStep = 0;
            int lastX = startX;
            int lastY = startY;

            await System.Threading.Tasks.Task.Run(() => {
                System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

                while (true) {
                    currentStep++;
                    double t = (double)currentStep / steps;
                    double ease = currentStep >= steps ? 1.0 : t * t * t; // easeInCubic

                    int currentX = (int)Math.Round(startX + (finalX - startX) * ease);
                    int currentY = (int)Math.Round(startY + (finalY - startY) * ease);

                    if (currentStep >= steps) {
                        currentX = finalX;
                        currentY = finalY;
                    }

                    if (currentX != lastX || currentY != lastY) {
                        SetWindowPos(hwnd, IntPtr.Zero, currentX, currentY, 0, 0,
                            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                        TaskTile.NativeMethods.DwmFlush();
                        lastX = currentX;
                        lastY = currentY;
                    }
                    else {
                        TaskTile.NativeMethods.DwmFlush();
                    }

                    if (currentStep >= steps) break;
                }
            });
        }
        
        if (TaskTile.Services.SettingsService.Current.StartPopupsInBackground) {
            var hwnd = WindowNative.GetWindowHandle(this);
            TaskTile.NativeMethods.ShowWindow(hwnd, 0); // SW_HIDE
            _isClosing = false; // Reset state for next use
        } else {
            this.Close();
        }
    }

    // ─── Events ──────────────────────────────────────────────────────────────
    void OnActivated(object s, WindowActivatedEventArgs e) { if (e.WindowActivationState != WindowActivationState.Deactivated) _activated = true; else if (_activated && !_keepOpen) _ = CloseWithFadeAsync(); }

    private void AppLabel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock tb) return;
        if (!_marqueeAppLabels) return;

        tb.Measure(new Windows.Foundation.Size(double.PositiveInfinity, 36));
        double desiredW = tb.DesiredSize.Width;
        double diff = desiredW - 170;
        if (diff > 4)
        {
            if (tb.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                tb.RenderTransform = tt;
            }

            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                From = 0,
                To = -(diff + 12),
                Duration = new Duration(TimeSpan.FromSeconds(Math.Max(2.5, diff / 20.0))),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(0.8)
            };
            Storyboard.SetTarget(anim, tb);
            Storyboard.SetTargetProperty(anim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(anim);
            sb.Begin();
            tb.Tag = sb;
        }
    }

    private void ListRow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_scrollAppLabels || _marqueeAppLabels) return;
        if (sender is not FrameworkElement el) return;
        var tb = FindVisualChild<TextBlock>(el);
        if (tb == null) return;

        tb.Measure(new Windows.Foundation.Size(double.PositiveInfinity, 36));
        double desiredW = tb.DesiredSize.Width;
        double diff = desiredW - 170;
        if (diff > 4)
        {
            if (tb.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                tb.RenderTransform = tt;
            }

            var sb = new Storyboard();
            var anim = new DoubleAnimation
            {
                To = -(diff + 8),
                Duration = new Duration(TimeSpan.FromSeconds(Math.Max(1.2, diff / 35.0))),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, tb);
            Storyboard.SetTargetProperty(anim, "(UIElement.RenderTransform).(TranslateTransform.X)");
            sb.Children.Add(anim);
            sb.Begin();
            el.Tag = sb;
        }
    }

    private void ListRow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_scrollAppLabels || _marqueeAppLabels) return;
        if (sender is not FrameworkElement el) return;
        if (el.Tag is Storyboard oldSb) oldSb.Stop();
        var tb = FindVisualChild<TextBlock>(el);
        if (tb == null) return;
        if (tb.RenderTransform is not TranslateTransform tt) return;

        var sb = new Storyboard();
        var anim = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromSeconds(0.35)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(anim, tb);
        Storyboard.SetTargetProperty(anim, "(UIElement.RenderTransform).(TranslateTransform.X)");
        sb.Children.Add(anim);
        sb.Begin();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void Icon_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is AppEntryViewModel vm)
        {
            var container = _compactR.ContainerFromItem(vm) as GridViewItem ?? _compactRVertical.ContainerFromItem(vm) as GridViewItem;
            if (container != null) Canvas.SetZIndex(container, 100);
        }
    }

    private void Icon_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is AppEntryViewModel vm)
        {
            var container = _compactR.ContainerFromItem(vm) as GridViewItem ?? _compactRVertical.ContainerFromItem(vm) as GridViewItem;
            if (container != null) Canvas.SetZIndex(container, 0);
        }
    }

    void App_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AppEntryViewModel vm && !string.IsNullOrEmpty(vm.ExePath))
        {
            try { Process.Start(new ProcessStartInfo(vm.ExePath) { UseShellExecute = true }); } catch { }
        }
        _ = CloseWithFadeAsync();
    }
    void Open_Click(object s, RoutedEventArgs e)
    {
        if (s is MenuFlyoutItem i && i.Tag is string p && !string.IsNullOrEmpty(p))
        { try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); } catch { } }
        _ = CloseWithFadeAsync();
    }
    private async void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group == null) return;

        var tb = new TextBox 
        { 
            Text = group.Name, 
            PlaceholderText = "Enter new group name", 
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        // select all text
        tb.Loaded += (s, ev) => { tb.SelectAll(); tb.Focus(FocusState.Programmatic); };

        var dialog = new ContentDialog
        {
            Title = "Rename App Group",
            Content = tb,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(tb.Text))
        {
            group.Name = tb.Text;
            if (_title != null) _title.Text = group.Name;
            
            // Save config so it persists
            TaskTile.Services.GroupService.Instance.Save();
            
            // Refresh main window if it's open
            if (App.MainWindowInstance != null)
            {
                // Force a layout update in main window if needed by just saving config,
                // the collection changed event doesn't fire for property updates,
                // but we can trigger a manual refresh or just let it update on next launch.
            }
        }
    }

    void EditGroup_Click(object s, RoutedEventArgs e)
    {
        if (App.MainWindowInstance != null)
        {
            App.MainWindowInstance.AppWindow.Show();
            App.MainWindowInstance.NavigateTo(typeof(TaskTile.Pages.GroupsPage));
        }
        _ = CloseWithFadeAsync();
    }
    void LaunchAll_Click(object s, RoutedEventArgs e)
    {
        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group?.Apps != null)
        {
            foreach (var app in group.Apps)
            {
                var exe = app.ExePath;
                if (!string.IsNullOrEmpty(exe))
                { try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); } catch { } }
            }
        }
        _ = CloseWithFadeAsync();
    }
    void RunAdmin_Click(object s, RoutedEventArgs e)
    {
        if (s is MenuFlyoutItem i && i.Tag is string p && !string.IsNullOrEmpty(p))
        { try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true, Verb = "runas" }); } catch { } }
        _ = CloseWithFadeAsync();
    }
    void OpenLoc_Click(object s, RoutedEventArgs e)
    {
        if (s is MenuFlyoutItem item && item.Tag is string p && !string.IsNullOrEmpty(p))
        {
            try
            {
                // For real files: /select highlights the item in Explorer
                // For non-file paths (UWP), fallback to just opening Explorer
                string arg = System.IO.File.Exists(p) ? $"/select,\"{p}\"" : $"\"{System.IO.Path.GetDirectoryName(p)}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
            }
            catch { }
        }
        _ = CloseWithFadeAsync();
    }
    void GoToPage(int page, int direction = 0)
    {
        if (page < 0 || page >= _classicPages.Count) return;
        _classicPage = page;
        
        // Scroll smoothly to the exact vertical offset for the target page
        // Each page is gH + 20 (content + padding) + 20 (margin gap) = gH + 40 tall
        double pageHeight = _classicSV.Height + 20; 
        
        _classicSV.ChangeView(null, page * pageHeight, null, false); // false = enable animation

        UpdateClassicPagingVisuals();
    }

    private bool _isAreaHovered = false;
    private int _hoveredDotIndex = -1;

    private void UpdateClassicPagingVisuals()
    {
        if (_pageDots == null) return;
        bool canGoUp = (_classicPage > 0);
        bool canGoDown = (_classicPage < _classicPages.Count - 1);

        foreach (var child in _pageDots.Children)
        {
            if (child is Grid g && g.Children.Count > 0 && g.Tag is string tag)
            {
                if (tag.StartsWith("dot_"))
                {
                    int pi = int.Parse(tag.Substring(4));
                    var dot = (Border)g.Children[0];
                    bool isDotHovered = (pi == _hoveredDotIndex);
                    bool isActive = (pi == _classicPage);

                    double targetSize = (isActive || isDotHovered) ? 6 : 4;
                    double targetOpacity = isDotHovered ? 1.0 : (_isAreaHovered ? 0.8 : 0.4);

                    dot.Width = targetSize;
                    dot.Height = targetSize;

                    var vis = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(dot);
                    var anim = vis.Compositor.CreateScalarKeyFrameAnimation();
                    anim.InsertKeyFrame(1f, (float)targetOpacity);
                    anim.Duration = TimeSpan.FromMilliseconds(150);
                    vis.StartAnimation("Opacity", anim);
                }
                else if (tag == "upArrow")
                {
                    var icon = (FontIcon)g.Children[0];
                    float targetOpacity = (_isAreaHovered && canGoUp) ? 1.0f : 0.0f;

                    var vis = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(icon);
                    var anim = vis.Compositor.CreateScalarKeyFrameAnimation();
                    anim.InsertKeyFrame(1f, targetOpacity);
                    anim.Duration = TimeSpan.FromMilliseconds(150);
                    vis.StartAnimation("Opacity", anim);

                    g.IsHitTestVisible = (_isAreaHovered && canGoUp);
                }
                else if (tag == "downArrow")
                {
                    var icon = (FontIcon)g.Children[0];
                    float targetOpacity = (_isAreaHovered && canGoDown) ? 1.0f : 0.0f;

                    var vis = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(icon);
                    var anim = vis.Compositor.CreateScalarKeyFrameAnimation();
                    anim.InsertKeyFrame(1f, targetOpacity);
                    anim.Duration = TimeSpan.FromMilliseconds(150);
                    vis.StartAnimation("Opacity", anim);

                    g.IsHitTestVisible = (_isAreaHovered && canGoDown);
                }
            }
        }
    }

    private void UpdateClassicPagingUI()
    {
        _pageDots.Children.Clear();

        if (_classicPages.Count <= 1)
        {
            _pageDots.Visibility = Visibility.Collapsed;
            return;
        }

        bool canGoUp = (_classicPage > 0);
        bool canGoDown = (_classicPage < _classicPages.Count - 1);

        // Up Arrow
        var upHitGrid = new Grid { Width = 16, Height = 12, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)), Tag = "upArrow" };
        var upArrow = new FontIcon
        {
            Glyph = "\uEDDB", // Caret Up Solid 8
            FontSize = 6,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0 // handled by visual state
        };
        upHitGrid.Children.Add(upArrow);
        upHitGrid.PointerPressed += (_, _) => { if (_classicPage > 0) GoToPage(_classicPage - 1, -1); };
        _pageDots.Children.Add(upHitGrid);

        // Dots
        for (int pi = 0; pi < _classicPages.Count; pi++)
        {
            int capture = pi;
            var hitGrid = new Grid { Width = 10, Height = 10, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)), Tag = "dot_" + pi };
            var dot = new Border
            {
                Width = (pi == _classicPage) ? 6 : 4,
                Height = (pi == _classicPage) ? 6 : 4,
                CornerRadius = new CornerRadius(99),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                Opacity = 0.4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            hitGrid.Children.Add(dot);

            hitGrid.PointerEntered += (s, e) => { _hoveredDotIndex = capture; UpdateClassicPagingVisuals(); };
            hitGrid.PointerExited += (s, e) => { if (_hoveredDotIndex == capture) _hoveredDotIndex = -1; UpdateClassicPagingVisuals(); };
            hitGrid.PointerPressed += (_, _) => GoToPage(capture, capture > _classicPage ? 1 : -1);
            _pageDots.Children.Add(hitGrid);
        }

        // Down Arrow
        var downHitGrid = new Grid { Width = 16, Height = 12, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0)), Tag = "downArrow" };
        var downArrow = new FontIcon
        {
            Glyph = "\uEDDC", // Caret Down Solid 8
            FontSize = 6,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0 // handled by visual state
        };
        downHitGrid.Children.Add(downArrow);
        downHitGrid.PointerPressed += (_, _) => { if (_classicPage < _classicPages.Count - 1) GoToPage(_classicPage + 1, 1); };
        _pageDots.Children.Add(downHitGrid);

        UpdateClassicPagingVisuals();
        _pageDots.Visibility = Visibility.Visible;
    }

    public void PageDots_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isAreaHovered = true;
        UpdateClassicPagingVisuals();
    }

    public void PageDots_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isAreaHovered = false;
        UpdateClassicPagingVisuals();
    }

    public void PlayPopInAnimation()
    {
        if (!_disableAnimation)
        {
            _popIn.Begin();
        }
    }

// ─── Infrastructure ───────────────────────────────────────────────────────────
}
public class CustomTemplate : Microsoft.UI.Xaml.IElementFactory
{
    readonly Func<UIElement> _f;
    public CustomTemplate(Func<UIElement> f) => _f = f;
    public UIElement GetElement(ElementFactoryGetArgs a) => _f();
    public void RecycleElement(ElementFactoryRecycleArgs a) { }
}