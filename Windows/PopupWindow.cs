using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

// ─── VariableGridView for Dynamic VariableSizedWrapGrid Sizing ────────────
public class VariableGridView : GridView
{
    public int MaxColumns { get; set; } = 3;

    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        if (element is UIElement ui && item is AppEntryViewModel app)
        {
            VariableSizedWrapGrid.SetColumnSpan(ui, app.ColSpan);
            VariableSizedWrapGrid.SetRowSpan(ui, app.RowSpan);
        }
        if (ItemsPanelRoot is VariableSizedWrapGrid vswg && vswg.MaximumRowsOrColumns != MaxColumns)
        {
            vswg.MaximumRowsOrColumns = MaxColumns;
        }
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("ScrollViewer") is ScrollViewer sv)
        {
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.VerticalScrollMode = ScrollMode.Disabled;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.HorizontalScrollMode = ScrollMode.Disabled;
            sv.ManipulationMode = ManipulationModes.None;
        }
    }
}

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
    private Visibility _editModeVisibility = Visibility.Collapsed;
    public Visibility EditModeVisibility
    {
        get => _editModeVisibility;
        set
        {
            if (_editModeVisibility != value)
            {
                _editModeVisibility = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(EditModeVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AdminShieldVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NotificationBadgeVisibility)));
            }
        }
    }
    private string _arguments = string.Empty;
    public string Arguments
    {
        get => _arguments;
        set { if (_arguments != value) { _arguments = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Arguments))); } }
    }
    private bool _runAsAdmin;
    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set
        {
            if (_runAsAdmin != value)
            {
                _runAsAdmin = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunAsAdmin)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AdminShieldVisibility)));
            }
        }
    }

    public Visibility AdminShieldVisibility =>
        (TaskTile.Services.SettingsService.Current.ShowAdminShieldBadge && _runAsAdmin && EditModeVisibility == Visibility.Collapsed)
        ? Visibility.Visible : Visibility.Collapsed;

    private int _notificationCount = 0;
    public int NotificationCount
    {
        get => _notificationCount;
        set
        {
            if (_notificationCount != value)
            {
                _notificationCount = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NotificationCount)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NotificationBadgeText)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NotificationBadgeVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(NotificationBadgeTooltip)));
            }
        }
    }

    public string NotificationBadgeText => _notificationCount > 99 ? "99+" : _notificationCount.ToString();

    public string NotificationBadgeTooltip => $"{_notificationCount} new notification{(_notificationCount == 1 ? "" : "s")}";

    public Visibility NotificationBadgeVisibility =>
        (TaskTile.Services.SettingsService.Current.ShowNotificationBadges && _notificationCount > 0 && EditModeVisibility == Visibility.Collapsed)
        ? Visibility.Visible : Visibility.Collapsed;

    private static readonly SolidColorBrush _goldShieldBrush = new(Windows.UI.Color.FromArgb(255, 255, 185, 0));
    private static readonly SolidColorBrush _neutralShieldBrush = new(Windows.UI.Color.FromArgb(255, 160, 160, 160));

    public Brush AdminShieldBrush
    {
        get
        {
            if (TaskTile.Services.SettingsService.Current.AdminShieldColor == 0)
            {
                return _goldShieldBrush;
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var res) && res is Brush b)
                {
                    return b;
                }
                return _neutralShieldBrush;
            }
        }
    }

    private int _indicatorStyle = 0; // 0 = Bar, 1 = Corner Dot
    public int IndicatorStyle
    {
        get => _indicatorStyle;
        set
        {
            if (_indicatorStyle != value)
            {
                _indicatorStyle = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IndicatorStyle)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunningBarVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunningDotVisibility)));
            }
        }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning != value)
            {
                _isRunning = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsRunning)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunningVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunningBarVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RunningDotVisibility)));
            }
        }
    }
    public Visibility RunningVisibility => _isRunning ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RunningBarVisibility => (_isRunning && _indicatorStyle == 0) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RunningDotVisibility => (_isRunning && _indicatorStyle == 1) ? Visibility.Visible : Visibility.Collapsed;
    private int _tileSize = 1; // 1 = 1x1, 2 = 2x1, 3 = 1x2, 4 = 2x2
    public int TileSize
    {
        get => _tileSize;
        set
        {
            if (_tileSize != value)
            {
                _tileSize = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileSize)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ColSpan)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(RowSpan)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileWidth)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileHeight)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SmallTileVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WideTileVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TallTileVisibility)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LargeTileVisibility)));
            }
        }
    }

    private int _tileSpacing = 8;
    public int TileSpacing
    {
        get => _tileSpacing;
        set
        {
            if (_tileSpacing != value)
            {
                _tileSpacing = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileSpacing)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileWidth)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TileHeight)));
            }
        }
    }
    public int ColSpan => (_tileSize == 2 || _tileSize == 4) ? 2 : 1;
    public int RowSpan => (_tileSize == 3 || _tileSize == 4) ? 2 : 1;
    public double TileWidth => ColSpan == 2 ? (152 + TileSpacing) : 76;
    public double TileHeight => RowSpan == 2 ? (144 + TileSpacing) : 72;

    public Visibility SmallTileVisibility => _tileSize == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility WideTileVisibility => _tileSize == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TallTileVisibility => _tileSize == 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LargeTileVisibility => _tileSize == 4 ? Visibility.Visible : Visibility.Collapsed;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

// Start Menu-style acrylic (matches AcrylicBackgroundFillColorBaseBrush exactly)
public class StartMenuAcrylicBackdrop : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController? _ctrl; SystemBackdropConfiguration? _cfg;
    bool _isDark; float _tintOp; float _lumOp;
    public StartMenuAcrylicBackdrop(bool isDark = true, float tintOp = 0.35f, float lumOp = 0.90f) { _isDark = isDark; _tintOp = tintOp; _lumOp = lumOp; }

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
    bool _isDark;
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

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll", EntryPoint = "SetClassLong")]
    static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);
    static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetClassLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }
    const int GCLP_HBRBACKGROUND = -10;
    [DllImport("gdi32.dll")]
    static extern IntPtr GetStockObject(int fnObject);
    const int BLACK_BRUSH = 4;

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
    const uint   SWP_SHOWWINDOW  = 0x0040;

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
    double          _classicPageHeight = 0;

    ObservableCollection<AppEntryViewModel> _appsList = new();
    bool _isInEditMode = false;
    bool _savedKeepOpen = false;

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
    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    static int GetRefreshIntervalMs(IntPtr hwnd)
    {
        try
        {
            IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (mon != IntPtr.Zero)
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
                if (GetMonitorInfo(mon, ref mi))
                {
                    var dm = new DEVMODE();
                    dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                    if (EnumDisplaySettings(mi.szDevice, -1, ref dm))
                    {
                        int freq = dm.dmDisplayFrequency;
                        if (freq > 0) return Math.Clamp(1000 / freq, 4, 16);
                    }
                }
            }
        }
        catch { }
        return 16;
    }

    private int _targetX, _targetY, _animStartX, _animStartY;
    private bool _isTop, _isLeft, _isRight;
    private System.Threading.CancellationTokenSource? _slideCts;
    SubclassProc? _subclassDelegate;

    // max visible apps per classic page
    const int PAGE_SIZE = 6;

    public PopupWindow(string groupId, string? initialAction = null)
    {
        _groupId = groupId;
        TaskTile.Services.SettingsService.Load();
        try { 
            ExtendsContentIntoTitleBar = true; 
            if (AppWindow.TitleBar != null) AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        } catch { }
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _subclassDelegate = new SubclassProc(WindowSubclass);
        SetWindowSubclass(hwnd, _subclassDelegate, 1, IntPtr.Zero);

        try
        {
            TaskTile.Services.JumpListService.SetWindowAppId(hwnd, TaskTile.Services.JumpListService.GetGroupAppId(_groupId));
        }
        catch { }

        TaskTile.Services.NotificationBadgeService.NotificationCountsChanged += OnNotificationCountsChanged;
        this.Closed += (s, e) => { TaskTile.Services.NotificationBadgeService.NotificationCountsChanged -= OnNotificationCountsChanged; };

        // Disable resize, remove titlebar and border, hide from taskbar
        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.IsResizable   = true; // REQUIRED for native Windows 11 DWM rounded corners
            op.IsMaximizable = false;
            op.IsMinimizable = false;
            op.IsAlwaysOnTop = true;
            op.SetBorderAndTitleBar(true, false); // REQUIRED for SystemBackdrop to not fail and turn black
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }
        
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
        _root.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ClassicSV_PointerWheelChanged), true);
        _classicSV.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ClassicSV_PointerWheelChanged), true);
        _holder.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ClassicSV_PointerWheelChanged), true);
        _classicPagesPanel.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ClassicSV_PointerWheelChanged), true);
        _classicPagesPanel.ManipulationMode = ManipulationModes.None;
        _holder.ManipulationMode = ManipulationModes.None;
        _classicSV.ViewChanged += (s, e) =>
        {
            if (!_isInEditMode && _classicSV.VerticalOffset != 0)
            {
                _classicSV.ChangeView(null, 0, null, true);
            }
        };
        _classicR.ContainerContentChanging += ClassicR_ContainerContentChanging;
        _modernR.ContainerContentChanging += ClassicR_ContainerContentChanging;
        _cardR.ContainerContentChanging += ClassicR_ContainerContentChanging;
        _compactR.ContainerContentChanging += ClassicR_ContainerContentChanging;
        _compactRVertical.ContainerContentChanging += ClassicR_ContainerContentChanging;
        _listR.ContainerContentChanging += ClassicR_ContainerContentChanging;

        _classicR.DragItemsStarting += ClassicR_DragItemsStarting;
        _classicR.DragOver += ClassicR_DragOver;
        _classicR.Drop += ClassicR_Drop;

        _modernR.DragItemsStarting += ClassicR_DragItemsStarting;
        _modernR.DragOver += ClassicR_DragOver;
        _modernR.Drop += ClassicR_Drop;

        _cardR.DragItemsStarting += ClassicR_DragItemsStarting;
        _cardR.DragOver += ClassicR_DragOver;
        _cardR.Drop += ClassicR_Drop;

        // Cloak the window BEFORE Activation or sizing to mask the compositor initialization
        var h = WindowNative.GetWindowHandle(this);
        
        LoadAndPosition(); // Call this BEFORE activating, so _dimWin doesn't steal focus later

        ApplyWindowFlags();
        this.Activated += OnActivated;
        if (!string.IsNullOrEmpty(_groupId))
        {
            this.Activate();
        }
        else
        {
            try { AppWindow.Hide(); } catch { }
        }

        // WinUI 3 resets DWM attributes on Activate(), so re-apply them IMMEDIATELY
        var hw = WindowNative.GetWindowHandle(this);
        ApplyWindowFlags();

        if (!string.IsNullOrEmpty(_groupId))
        {
            var trayHwnd = FindWindow("Shell_TrayWnd", null);
            var insertAfter = trayHwnd != IntPtr.Zero ? trayHwnd : IntPtr.Zero;
            SetWindowPos(hw, insertAfter, _animStartX, _animStartY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;
            StartEntranceAnimation(hw, _animStartX, _animStartY, _targetX - borderX, _targetY);
        }

        if (initialAction == "edit")
        {
            DispatcherQueue.TryEnqueue(() => EnterEditMode());
        }
        else if (initialAction == "rename")
        {
            DispatcherQueue.TryEnqueue(() => BeginInlineRename());
        }
    }

    void ConfigureEntranceAnimation()
    {
        _popInOpacity.From = 0.6;
        _popInOpacity.To = 1.0;
        _popInOpacity.Duration = new Duration(TimeSpan.FromMilliseconds(160));

        _popInTranslateY.From = 0.0;
        _popInTranslateY.To = 0.0;
        _popInTranslateY.Duration = new Duration(TimeSpan.FromMilliseconds(160));

        _popInTranslateX.From = 0.0;
        _popInTranslateX.To = 0.0;
        _popInTranslateX.Duration = new Duration(TimeSpan.FromMilliseconds(160));

        _popInScaleX.From = 1.0;
        _popInScaleX.To = 1.0;
        _popInScaleX.Duration = new Duration(TimeSpan.FromMilliseconds(160));

        _popInScaleY.From = 1.0;
        _popInScaleY.To = 1.0;
        _popInScaleY.Duration = new Duration(TimeSpan.FromMilliseconds(160));
    }

    private void StartEntranceAnimation(IntPtr hwnd, int startX, int startY, int finalX, int finalY)
    {
        if (!_disableAnimation)
        {
            ConfigureEntranceAnimation();
            _root.Opacity = 1.0;
            _popIn.Begin();
            AnimateEntrance(hwnd, startX, startY, finalX, finalY);
        }
        else
        {
            SetWindowPos(hwnd, HWND_TOPMOST, finalX, finalY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            _root.Opacity = 1;
            _rootScale.ScaleX = 1;
            _rootScale.ScaleY = 1;
            _rootTranslate.X = 0;
            _rootTranslate.Y = 0;
        }
    }

    private void AnimateEntrance(IntPtr hwnd, int startX, int startY, int finalX, int finalY)
    {
        _slideCts?.Cancel();
        _slideCts = new System.Threading.CancellationTokenSource();
        var cts = _slideCts;

        var trayHwnd = FindWindow("Shell_TrayWnd", null);
        IntPtr insertAfter = trayHwnd != IntPtr.Zero ? trayHwnd : IntPtr.Zero;

        // Authentic v0.5.5 / v0.6 15-step easeOutCubic entrance animation
        System.Threading.Tasks.Task.Run(() =>
        {
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;

            int steps = 15;
            int currentStep = 0;

            while (!cts.IsCancellationRequested)
            {
                currentStep++;
                double t = (double)currentStep / steps;
                double ease = currentStep >= steps ? 1.0 : 1.0 - Math.Pow(1.0 - t, 3.0); // easeOutCubic

                int currentX = (int)Math.Round(startX + (finalX - startX) * ease);
                int currentY = (int)Math.Round(startY + (finalY - startY) * ease);

                if (currentStep >= steps)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, finalX, finalY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
                    break;
                }

                SetWindowPos(hwnd, insertAfter, currentX, currentY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
                DwmFlush();
            }
        });
    }

    void ApplyWindowFlags()
    {
        var h  = WindowNative.GetWindowHandle(this);
        int policy = _disableRoundedCorners ? 1 : DWMWCP_ROUND;
        DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref policy, sizeof(int));

        int dark = _popupIsDark ? 1 : 0;
        DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
        
        int customBorderColor = _popupIsDark ? 0x00363636 : 0x00D6D6D6;
        
        if (_overrideBorderColor && !string.IsNullOrEmpty(_customBorderColor) && _customBorderColor.Length >= 7)
        {
            byte r = byte.Parse(_customBorderColor.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(_customBorderColor.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(_customBorderColor.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            customBorderColor = b << 16 | g << 8 | r;
        }
        DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref customBorderColor, sizeof(int));

        try
        {
            SetClassLongPtr(h, GCLP_HBRBACKGROUND, GetStockObject(BLACK_BRUSH));
        }
        catch { }
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

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
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
        if (_isInEditMode) return; // In edit mode, allow ScrollViewer to handle wheel scrolling

        pArgs.Handled = true; // Always consume wheel event so free-scrolling never happens!
        if (_classicPages.Count <= 1) return;

        long now = Environment.TickCount64;
        if (now - _lastWheelTicks < 180)
        {
            return;
        }

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

    private void UpdateRunningStatesAsync()
    {
        var items = _appsList.ToList();
        if (items.Count == 0) return;

        var dispatcher = this.DispatcherQueue;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.ExePath)) continue;
                bool running = TaskTile.Services.AppRunningService.IsAppRunning(item.ExePath);
                if (item.IsRunning != running)
                {
                    dispatcher?.TryEnqueue(() => item.IsRunning = running);
                }
            }
        });
    }

    // ─── Load data + position ───────────────────────────────────────────────
    public void LoadGroup(string groupId, string? initialAction = null)
    {
        _groupId = groupId;
        _isInEditMode = false;
        if (_editModeHeaderPanel != null) _editModeHeaderPanel.Visibility = Visibility.Collapsed;
        try
        {
            var h = WindowNative.GetWindowHandle(this);
            TaskTile.Services.JumpListService.SetWindowAppId(h, TaskTile.Services.JumpListService.GetGroupAppId(_groupId));
        }
        catch { }
        LoadAndPosition();
        
        // Grayscale taskbar icon: apply when popup opens for a pinned group
        if (TaskTile.Services.SettingsService.Current.UseGrayscaleTaskbarIcon && !string.IsNullOrEmpty(_groupId))
        {
            var grp = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
            if (grp != null) TaskTile.Services.TaskbarService.ApplyGrayscaleTaskbarIcon(grp);
        }

        // RESET PAGE
        _classicPage = 0;
        _classicSV.ChangeView(null, 0, null, true);
        if (_classicPagesPanel.RenderTransform is TranslateTransform tt) tt.Y = 0;
        UpdateClassicPagingVisuals();

        int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;

        var hwnd = WindowNative.GetWindowHandle(this);
        ApplyWindowFlags();
        var trayHwnd = FindWindow("Shell_TrayWnd", null);
        var insertAfter = trayHwnd != IntPtr.Zero ? trayHwnd : IntPtr.Zero;
        SetWindowPos(hwnd, insertAfter, _animStartX, _animStartY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        StartEntranceAnimation(hwnd, _animStartX, _animStartY, _targetX - borderX, _targetY);

        if (initialAction == "edit")
        {
            DispatcherQueue.TryEnqueue(() => EnterEditMode());
        }
        else if (initialAction == "rename")
        {
            DispatcherQueue.TryEnqueue(() => BeginInlineRename());
        }
    }

    void LoadAndPosition()
    {
        _appsList.Clear();
        string name="Group"; bool hideName=false, hideAppLabels=false, showCardLabels=false; int popupStyle=0,compactAlign=0,gridCols=3,gridRows=3,themeOverride=0,appIconStyle=0;
        bool launchAtCenter=false, makeMainFocus=false, overrideLaunchSide=false; int groupLaunchSide=0;
        bool disableAnimation = false, disableAutoHide = false, disableFloat = false, disableRoundedCorners = false, keepOpen = false;
        int groupTitleAlign = -1;
        int taskbarOffset = 8;
        int tileSpacing = 8;

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
                    if (g.TryGetProperty("TaskbarOffset", out var pOffset)) taskbarOffset = pOffset.GetInt32();
                    if (g.TryGetProperty("TileSpacing", out var pSpacing)) tileSpacing = pSpacing.GetInt32();

                    int groupIndicatorStyle = -1;
                    if (g.TryGetProperty("RunningIndicatorStyle", out var pInd)) groupIndicatorStyle = pInd.GetInt32();
                    int effectiveIndicatorStyle = groupIndicatorStyle >= 0 ? groupIndicatorStyle : TaskTile.Services.SettingsService.Current.RunningIndicatorStyle;

                    bool isDynamicFolder = false;
                    string dynamicFolderPath = string.Empty;
                    if (g.TryGetProperty("IsDynamicFolder", out p)) isDynamicFolder = p.GetBoolean();
                    if (g.TryGetProperty("DynamicFolderPath", out p)) dynamicFolderPath = p.GetString() ?? "";

                    bool uwp = appIconStyle == 1;
                    bool monotone = appIconStyle == 2;
                    bool onetone = appIconStyle == 3;

                    if (isDynamicFolder && !string.IsNullOrEmpty(dynamicFolderPath) && System.IO.Directory.Exists(dynamicFolderPath))
                    {
                        var entries = System.IO.Directory.GetFileSystemEntries(dynamicFolderPath);
                        foreach (var f in entries)
                        {
                            try
                            {
                                var attr = System.IO.File.GetAttributes(f);
                                if ((attr & (System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System)) != 0)
                                    continue;
                            }
                            catch { continue; }
                                
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
                                TileCornerRadius   = uwp ? new CornerRadius(0) : new CornerRadius(10),
                                EditModeVisibility = _isInEditMode ? Visibility.Visible : Visibility.Collapsed,
                                TileSize           = 1,
                                TileSpacing        = tileSpacing,
                                IndicatorStyle     = effectiveIndicatorStyle,
                                NotificationCount  = TaskTile.Services.NotificationBadgeService.GetNotificationCount(System.IO.Path.GetFileName(f), f)
                            };
                            _appsList.Add(entry);

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
                            string appArgs = a.TryGetProperty("Arguments", out var pArgs) ? (pArgs.GetString() ?? "") : "";
                            bool appRunAdm = a.TryGetProperty("RunAsAdmin", out var pAdm) && pAdm.GetBoolean();
                            int tileSize = 1;
                            if (a.TryGetProperty("TileSize", out var pSize)) tileSize = pSize.GetInt32();

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
                                Arguments          = appArgs,
                                RunAsAdmin         = appRunAdm,
                                IconImage          = initialIcon,
                                NormalVisibility   = monotone ? Visibility.Collapsed : Visibility.Visible,
                                MonotoneVisibility = monotone ? Visibility.Visible   : Visibility.Collapsed,
                                LabelVisibility    = hideAppLabels ? Visibility.Collapsed : Visibility.Visible,
                                LabelTrimming      = (marqueeAppLabels || scrollAppLabels) ? TextTrimming.None : TextTrimming.CharacterEllipsis,
                                UwpBackground      = uwp ? accentBrush : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                                TileCornerRadius   = uwp ? new CornerRadius(0) : new CornerRadius(10),
                                EditModeVisibility = _isInEditMode ? Visibility.Visible : Visibility.Collapsed,
                                TileSize           = tileSize,
                                TileSpacing        = tileSpacing,
                                IndicatorStyle     = effectiveIndicatorStyle,
                                NotificationCount  = TaskTile.Services.NotificationBadgeService.GetNotificationCount(a.GetProperty("Name").GetString()!, exe)
                            };
                            _appsList.Add(entry);

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

        UpdateRunningStatesAsync();
        RefreshNotificationCounts();

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
            // ypx was TORTURED to insanity here ;_) whoever decided DwmSetWindowAttribute(38) should clash with WinUI DesktopAcrylicController owes me 20 hours of my life back
            int dwmBackdrop = 1; // DWMSBT_NONE
            DwmSetWindowAttribute(hwnd, 38, ref dwmBackdrop, sizeof(int));
        }
        else
        {
            _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            if (_backdropStyle == 1)
            {
                SystemBackdrop = new MicaBackdropAlways(forceDark, MicaKind.Base);
            }
            else if (_backdropStyle == 2)
            {
                SystemBackdrop = new MicaBackdropAlways(forceDark, MicaKind.BaseAlt);
            }
            else
            {
                SystemBackdrop = new StartMenuAcrylicBackdrop(forceDark, forceDark ? 0.35f : 0.25f, forceDark ? 0.90f : 0.95f);
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
        if (_titleContainer != null) _titleContainer.Visibility = noTitle ? Visibility.Collapsed : Visibility.Visible;
        if (_headerEditBtn != null) _headerEditBtn.Visibility = _isInEditMode ? Visibility.Visible : Visibility.Collapsed;
        if (!noTitle) _title.Text = name;
        if (_titleEditBox != null) _titleEditBox.Visibility = Visibility.Collapsed;

        int alignPref = groupTitleAlign != -1 ? groupTitleAlign : s.TitleAlignment;
        HorizontalAlignment titleAlign = HorizontalAlignment.Center;
        if (alignPref == 0) titleAlign = HorizontalAlignment.Left;
        else if (alignPref == 2) titleAlign = HorizontalAlignment.Right;
        if (_titleContainer != null) _titleContainer.HorizontalAlignment = titleAlign;

        int n = _appsList.Count;
        if (n == 0) return;

        double logW = 0, logH = 0;

        if (popupStyle == 0) // Classic
        {
            _title.FontSize = 13;
            _classicSV.Visibility  = Visibility.Visible;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;

            // Completely disable ScrollViewer scrolling and hide any scrollbars
            _classicSV.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _classicSV.VerticalScrollMode = ScrollMode.Disabled;
            _classicSV.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _classicSV.HorizontalScrollMode = ScrollMode.Disabled;
            _classicSV.ManipulationMode = ManipulationModes.None;

            int cols = gridCols > 0 ? gridCols : 3;
            int rowMax = gridRows > 0 ? gridRows : 3;
            int pageSize = cols * rowMax;

            // Compact tile & cell sizing designed for a native square 3x3 silhouette
            int tileW = 76;
            int tileH = 72;
            int cellW = tileW + tileSpacing;
            int cellH = tileH + tileSpacing;
            
            int visibleRows = (n <= pageSize) ? (int)Math.Ceiling((double)n / cols) : rowMax;
            if (visibleRows <= 0) visibleRows = 1;
            int gW = cols * cellW;
            int gH = visibleRows * cellH;

            _classicPageHeight = gH;
            _classicPages.Clear();
            _classicPage = 0;
            _classicSV.ChangeView(null, 0, null, true);
            if (_classicPagesPanel.RenderTransform is TranslateTransform ttReset) ttReset.Y = 0;

            var classicWrapPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                $"<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><VariableSizedWrapGrid ItemWidth=\"{cellW}\" ItemHeight=\"{cellH}\" Orientation=\"Horizontal\" MaximumRowsOrColumns=\"{cols}\" /></ItemsPanelTemplate>");
            _classicR.ItemsPanel = classicWrapPanel;

            bool hasMultiplePages = n > pageSize;
            int padSide = 18;
            int padTop = 16;
            int padBottom = 16;
            _border.Padding = new Thickness(padSide, padTop, padSide, padBottom);
            _headerGrid.Margin = new Thickness(0, 0, 0, 10);

            if (!hasMultiplePages)
            {
                _classicR.MaxColumns = cols;
                if (_classicR.ItemsPanelRoot is VariableSizedWrapGrid vswg) vswg.MaximumRowsOrColumns = cols;
                _classicR.Width = gW;
                _classicR.Height = gH;
                _classicR.ItemContainerTransitions = CreateEntranceTransitions();
                ScrollViewer.SetVerticalScrollBarVisibility(_classicR, ScrollBarVisibility.Disabled);
                ScrollViewer.SetVerticalScrollMode(_classicR, ScrollMode.Disabled);
                ScrollViewer.SetHorizontalScrollBarVisibility(_classicR, ScrollBarVisibility.Disabled);
                ScrollViewer.SetHorizontalScrollMode(_classicR, ScrollMode.Disabled);
                _classicR.ManipulationMode = ManipulationModes.None;
                _classicR.ItemsSource = null;
                _classicR.ItemsSource = _appsList;
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
                    var page = _appsList.Skip(i).Take(pageSize).ToList();
                    _classicPages.Add(page);
                    var gv = new VariableGridView {
                        MaxColumns = cols,
                        ItemContainerTransitions = CreateEntranceTransitions(),
                        ItemsSource = page,
                        Width = gW,
                        Height = gH,
                        Margin = new Thickness(0),
                        Padding = new Thickness(0),
                        ItemTemplate = _classicR.ItemTemplate,
                        ItemContainerStyle = _classicR.ItemContainerStyle,
                        ItemsPanel = classicWrapPanel,
                        SelectionMode = ListViewSelectionMode.None,
                        IsItemClickEnabled = true,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ManipulationMode = ManipulationModes.None
                    };
                    ScrollViewer.SetVerticalScrollBarVisibility(gv, ScrollBarVisibility.Disabled);
                    ScrollViewer.SetVerticalScrollMode(gv, ScrollMode.Disabled);
                    ScrollViewer.SetHorizontalScrollBarVisibility(gv, ScrollBarVisibility.Disabled);
                    ScrollViewer.SetHorizontalScrollMode(gv, ScrollMode.Disabled);
                    gv.ContainerContentChanging += ClassicR_ContainerContentChanging;
                    gv.ItemClick += App_ItemClick;
                    gv.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ClassicSV_PointerWheelChanged), true);
                    _classicPagesPanel.Children.Add(gv);
                }

                UpdateClassicPagingUI();
            }

            _classicSV.Margin = new Thickness(0);
            _classicR.Padding = new Thickness(0); 
            _classicSV.Width = gW;
            _classicSV.Height = gH;
            _classicSV.MaxHeight = double.PositiveInfinity;
            _classicSV.VerticalScrollBarVisibility = hasMultiplePages ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
            _classicSV.VerticalScrollMode = ScrollMode.Disabled;
            _classicSV.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _classicSV.HorizontalScrollMode = ScrollMode.Disabled;
            _classicSV.ManipulationMode = ManipulationModes.None;

            _content.Padding = new Thickness(0);
            _title.HorizontalAlignment = titleAlign;
            _title.Margin = new Thickness(0);
            if (_titleContainer != null)
            {
                _titleContainer.HorizontalAlignment = titleAlign;
                _titleContainer.Margin = new Thickness(0);
            }
            _holder.HorizontalAlignment = HorizontalAlignment.Center;
            _holder.VerticalAlignment = VerticalAlignment.Center;
            
            logW = gW + _border.Padding.Left + _border.Padding.Right;
            int headerH = noTitle ? 0 : (20 + (int)_headerGrid.Margin.Bottom);
            logH = headerH + gH + _border.Padding.Top + _border.Padding.Bottom;
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
                _compactR.ItemContainerTransitions = CreateEntranceTransitions();
                _compactR.ItemsSource = null;
                _compactR.ItemsSource = _appsList;
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
                _compactRVertical.ItemContainerTransitions = CreateEntranceTransitions();
                _compactRVertical.ItemsSource = null;
                _compactRVertical.ItemsSource = _appsList;
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
            _border.Padding = new Thickness(10, 8, 10, 8);
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Visible;
            _pageDots.Visibility   = Visibility.Collapsed;
            _modernR.ItemContainerTransitions = CreateEntranceTransitions();
            _modernR.ItemsSource   = null;
            _modernR.ItemsSource   = _appsList;

            int requestedCols = gridCols > 0 ? gridCols : 3;
            int cols = Math.Min(n, requestedCols);
            if (cols == 0) cols = 1;
            int allRows = (int)Math.Ceiling((double)n / cols);
            
            int cell = 52 + tileSpacing;
            int gW = cols * cell;
            int gH = allRows * cell;

            _modernR.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                $"<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><ItemsWrapGrid ItemWidth=\"{cell}\" ItemHeight=\"{cell}\" Orientation=\"Horizontal\" HorizontalAlignment=\"Center\" /></ItemsPanelTemplate>");

            _modernR.Width = gW;
            _modernR.Height = gH;
            _modernR.Margin = new Thickness(0);
            _modernR.Padding = new Thickness(0);
            _modernR.HorizontalAlignment = HorizontalAlignment.Center;
            _modernR.VerticalAlignment = VerticalAlignment.Center;
            _holder.HorizontalAlignment = HorizontalAlignment.Center;
            _holder.VerticalAlignment = VerticalAlignment.Center;

            _title.HorizontalAlignment = titleAlign;
            _title.Margin = new Thickness(0, 0, 0, 4);
            if (_titleContainer != null)
            {
                _titleContainer.HorizontalAlignment = titleAlign;
                _titleContainer.Margin = _title.Margin;
            }

            _content.Padding = new Thickness(0);
            
            logW = gW + 20;
            int titleH = noTitle ? 0 : 32;
            logH = titleH + gH + 16;
            
            _modernSV.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _modernSV.VerticalScrollMode = ScrollMode.Disabled;
            _modernSV.ManipulationMode = ManipulationModes.None;
        }
        else if (popupStyle == 3) // List
        {
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _cardContainer.Visibility = Visibility.Collapsed;
            _pageDots.Visibility   = Visibility.Collapsed;
            
            _listSV.Visibility     = Visibility.Visible;
            _listR.ItemContainerTransitions = CreateEntranceTransitions();
            _listR.ItemsSource     = null;
            _listR.ItemsSource     = _appsList;
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
            if (_titleContainer != null)
            {
                _titleContainer.HorizontalAlignment = titleAlign;
                _titleContainer.Margin = _title.Margin;
            }

            logW = 260;

            int overhead = noTitle ? 8 : (24 + 14 + 14); // 52px total overhead gives title comfortable breathing room
            logH = overhead + n * 36;
        }

        else // Dialog-ish / Start Menu Folder Style
        {
            _classicSV.Visibility  = Visibility.Collapsed;
            _compactR.Visibility   = Visibility.Collapsed;
            _modernSV.Visibility   = Visibility.Collapsed;
            _listSV.Visibility     = Visibility.Collapsed;
            _pageDots.Visibility   = Visibility.Collapsed;
            _title.Visibility      = Visibility.Collapsed; // Title lives in the bottom bar
            if (_titleContainer != null) _titleContainer.Visibility = Visibility.Collapsed;

            _cardContainer.Visibility = Visibility.Visible;
            _cardFooterName.Text      = name;
            _cardFooterName.HorizontalAlignment = HorizontalAlignment.Left;
            _cardFooterName.Margin    = new Thickness(4, 0, 0, 0);

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
            _cardFooterBorder.Padding = new Thickness(12, 6, 8, 6);
            _cardFooterBorder.BorderThickness = new Thickness(0, 1, 0, 0);
            _cardFooterBorder.BorderBrush = new SolidColorBrush(forceDark 
                ? Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF) 
                : Windows.UI.Color.FromArgb(0x18, 0x00, 0x00, 0x00));
            _cardFooterBorder.Background = new SolidColorBrush(forceDark 
                ? Windows.UI.Color.FromArgb(0x25, 0x00, 0x00, 0x00) 
                : Windows.UI.Color.FromArgb(0x35, 0x00, 0x00, 0x00));

            // Respect showCardLabels or default to showing labels if not hidden
            bool showLabels = showCardLabels || (!hideAppLabels && n > 0);
            foreach (var app in _appsList)
                app.LabelVisibility = showLabels ? Visibility.Visible : Visibility.Collapsed;

            _cardR.ItemContainerTransitions = CreateEntranceTransitions();
            _cardR.ItemsSource = null;
            _cardR.ItemsSource = _appsList;

            int cellW = showLabels ? 150 : 52;
            int cellH = showLabels ? 38 : 52;
            int requestedCols = gridCols > 0 ? gridCols : (showLabels ? 2 : 3);
            int cols = Math.Min(n, requestedCols);
            if (cols == 0) cols = showLabels ? 2 : 3;
            int allRows = (int)Math.Ceiling((double)n / cols);

            int itemW = cellW + tileSpacing;
            int itemH = cellH + tileSpacing;
            int gridW = cols * itemW + 16;
            int gridH = allRows * itemH + 12;
            
            _cardR.Width = gridW;
            _cardR.Height = gridH;
            _cardR.Padding = new Thickness(8, 6, 8, 6);
            _cardR.HorizontalAlignment = HorizontalAlignment.Center;
            
            _cardR.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                $"<ItemsPanelTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><ItemsWrapGrid ItemWidth=\"{itemW}\" ItemHeight=\"{itemH}\" Orientation=\"Horizontal\" HorizontalAlignment=\"Center\" /></ItemsPanelTemplate>");

            logW = gridW;
            logH = gridH + 44; // Comfortable chin with footer
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

        double scale = _root.XamlRoot?.RasterizationScale ?? (double)sc;
        if (scale <= 0.1) scale = (double)sc;
        int physW = (int)Math.Round(logW * scale);
        int physH = (int)Math.Round(logH * scale);

        if (disableRoundedCorners) { _border.CornerRadius = new CornerRadius(0); _border.BorderThickness = new Thickness(0); }
        if (disableAnimation || !TaskTile.Services.SettingsService.Current.EnableAppEntranceAnimation) {
            _modernR.ItemContainerTransitions = null;
            _classicR.ItemContainerTransitions = null;
            _compactR.ItemContainerTransitions = null;
            _compactRVertical.ItemContainerTransitions = null;
            _listR.ItemContainerTransitions = null;
            _cardR.ItemContainerTransitions = null;
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

            int effectiveGap = taskbarOffset >= 0 ? taskbarOffset : s.TaskbarGap;
            if (_disableFloat) effectiveGap = 0;
            int gap = (int)Math.Round(effectiveGap * sc);
            var trayHwnd = FindWindow("Shell_TrayWnd", null);
            RECT trayRect = default;
            bool hasTray = trayHwnd != IntPtr.Zero && GetWindowRect(trayHwnd, out trayRect);

            APPBARDATA abd = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
            nint state = SHAppBarMessage(ABM_GETSTATE, ref abd);
            bool isAutoHidden = (state.ToInt32() & ABS_AUTOHIDE) != 0;
            int autoHideBuffer = isAutoHidden ? (int)Math.Round(48 * sc) : 0;

            int trayWidth = trayRect.Right - trayRect.Left;
            int trayHeight = trayRect.Bottom - trayRect.Top;
            bool trayIsTop = hasTray && trayRect.Top <= outer.Y + 20 && trayHeight < (outer.Height / 2) && trayWidth > trayHeight;
            if (!isLeft && !isRight && (launchSide == TaskTile.Models.LaunchSide.Top || work.Y > outer.Y || trayIsTop))
            {
                isTop = true;
            }

            int topBaseY = (hasTray && trayIsTop && trayRect.Bottom > outer.Y) ? Math.Max(trayRect.Bottom, work.Y) : work.Y;

            if      (isLeft)  { x = work.X + gap;                                              y = pt.Y - physH / 2; }
            else if (isRight) { x = work.X + work.Width - physW - gap;                         y = pt.Y - physH / 2; }
            else if (isTop)   { y = topBaseY + gap + autoHideBuffer;                           x = pt.X - physW / 2; }
            else
            {
                // Bottom taskbar: check physical trayRect if available to avoid Windows 11 Beta / Windhawk 12px overlap bug
                int bottomBaseY = (hasTray && trayRect.Top > work.Y) ? trayRect.Top : (work.Y + work.Height);
                y = bottomBaseY - physH - gap - autoHideBuffer;
                x = pt.X - physW / 2;
            }

            if (_disableFloat) {
                if (isLeft) x = work.X;
                else if (isRight) x = work.X + work.Width - physW;
                else if (isTop) y = topBaseY;
                else y = (hasTray && trayRect.Top > work.Y ? trayRect.Top : (work.Y + work.Height)) - physH - autoHideBuffer;
                
                if (!_disableRoundedCorners) {
                    _border.CornerRadius = isTop ? new CornerRadius(0, 0, 4, 4) : isLeft ? new CornerRadius(0, 4, 4, 0) : isRight ? new CornerRadius(4, 0, 0, 4) : new CornerRadius(4, 4, 0, 0);
                    _border.BorderThickness = new Thickness(0);
                }
            }

            x = Math.Clamp(x, work.X, work.X + work.Width  - physW);
            int minClampY = isTop ? topBaseY : work.Y;
            y = Math.Clamp(y, minClampY, work.Y + work.Height - physH - autoHideBuffer);

            // Configure launch-side-aware pop-in translation
            if (!_isDesktopMode && _popIn != null)
            {
                if (!_disableAnimation && !_activated)
                {
                    if (_popInTranslateY != null) _popInTranslateY.From = isTop ? -12 : 12;
                    if (_popInTranslateX != null) _popInTranslateX.From = isLeft ? -12 : isRight ? 12 : 0;
                    _root.Opacity = 0;
                }
                else
                {
                    _root.Opacity = 1.0;
                }
            }
        }

        _isTop = isTop;
        _isLeft = isLeft;
        _isRight = isRight;

        // Calculate invisible borders to offset the Move, ensuring the visible content is perfectly positioned
        int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;

        // Set target positions
        _targetX = x;
        _targetY = y;

        int finalX = _targetX - borderX;
        int finalY = _targetY;
        int startX = finalX;
        int startY = finalY;

        if (!_disableAnimation)
        {
            int animDistance = 40;
            if (_isTop) startY -= animDistance;
            else if (_isLeft) startX -= animDistance;
            else if (_isRight) startX += animDistance;
            else startY += animDistance; // bottom default
        }

        _animStartX = startX;
        _animStartY = startY;

        // Use ResizeClient to guarantee exact client area, preventing right-edge clipping.
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(physW, physH));
        
        // Position OS window initially at anim start position (or restore final position if already active)
        if (_activated)
        {
            AppWindow.Move(new Windows.Graphics.PointInt32(finalX, finalY));
        }
        else
        {
            AppWindow.Move(new Windows.Graphics.PointInt32(_animStartX, _animStartY));
        }

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

        if (_disableAnimation || !TaskTile.Services.SettingsService.Current.EnableAppEntranceAnimation) 
        { 
            if (this.Content is FrameworkElement root) root.Transitions?.Clear(); 
            _classicR.ItemContainerTransitions = null;
            _modernR.ItemContainerTransitions = null;
            _compactR.ItemContainerTransitions = null;
            _compactRVertical.ItemContainerTransitions = null;
            _listR.ItemContainerTransitions = null;
            _cardR.ItemContainerTransitions = null;
        }

        ApplyGlowUIOrFluentTheme();
    }

    private void ApplyGlowUIOrFluentTheme()
    {
        TaskTile.Services.SettingsService.Load();
        bool mix = TaskTile.Services.SettingsService.Current.YpxMixUI;

        if (mix)
        {
            // FireCube GlowUI: dynamic System Accent luminous border
            _border.BorderBrush = TaskTile.Helpers.GlowUITheme.CreateGlowBorderBrush(isHover: false);
            _border.BorderThickness = new Thickness(1.2);
            if (!_disableRoundedCorners)
            {
                _border.CornerRadius = new CornerRadius(12);
            }

            // Tile hover FireCube Glow styling (accent border + soft accent tint)
            var glowHoverBorder = TaskTile.Helpers.GlowUITheme.CreateGlowBorderBrush(isHover: true);
            var glowHoverBg = TaskTile.Helpers.GlowUITheme.CreateTileHoverBackgroundBrush();

            _root.Resources["TileControlStrokeDefault"] = glowHoverBorder;
            _root.Resources["ControlElevationBorderBrush"] = glowHoverBorder;
            _root.Resources["TileControlFillSecondary"] = glowHoverBg;
            _root.Resources["SubtleFillColorSecondaryBrush"] = glowHoverBg;
        }
        else
        {
            // Normal WinUI 3: Standard native Fluent Design
            _border.ClearValue(Border.BorderBrushProperty);
            _border.BorderThickness = new Thickness(0);
            if (!_disableRoundedCorners)
            {
                _border.CornerRadius = new CornerRadius(8);
            }

            // Restore standard WinUI 3 brushes
            _root.Resources.Remove("TileControlStrokeDefault");
            _root.Resources.Remove("ControlElevationBorderBrush");
            _root.Resources.Remove("TileControlFillSecondary");
            _root.Resources.Remove("SubtleFillColorSecondaryBrush");
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

        _slideCts?.Cancel();
        _slideCts = null;

        if (_taskbarTracker != null)
        {
            _taskbarTracker.Stop();
        }

        try
        {
            if (_dimWin?.Content is Grid dg)
            {
                var daDim = new DoubleAnimation { To = 0, Duration = new Duration(TimeSpan.FromMilliseconds(80)) };
                var sbDim = new Storyboard();
                Storyboard.SetTarget(daDim, dg);
                Storyboard.SetTargetProperty(daDim, "Opacity");
                sbDim.Children.Add(daDim);
                sbDim.Begin();
            }

            if (!_disableAnimation && _root != null)
            {
                var hwnd = WindowNative.GetWindowHandle(this);
                uint dpi = GetDpiForWindow(hwnd);
                float sc = dpi == 0 ? 1f : dpi / 96f;
                int slideDist = (int)Math.Round(24 * sc);

                int borderX = (AppWindow.Size.Width - AppWindow.ClientSize.Width) / 2;
                int startX = _targetX - borderX;
                int startY = _targetY;
                int endX = startX;
                int endY = startY;

                if (_isTop) endY -= slideDist;
                else if (_isLeft) endX -= slideDist;
                else if (_isRight) endX += slideDist;
                else endY += slideDist;

                var fadeOut = new DoubleAnimation { To = 0, Duration = new Duration(TaskTile.Helpers.AnimationHelper.ScaleDuration(TimeSpan.FromMilliseconds(110))) };
                var sb = new Storyboard();
                Storyboard.SetTarget(fadeOut, _root);
                Storyboard.SetTargetProperty(fadeOut, "Opacity");
                sb.Children.Add(fadeOut);
                sb.Begin();

                int intervalMs = GetRefreshIntervalMs(hwnd);
                int durationMs = (int)Math.Round(TaskTile.Helpers.AnimationHelper.ScaleDurationMs(110.0));
                int steps = Math.Max(1, durationMs / intervalMs);
                int currentStep = 0;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
                    while (currentStep < steps)
                    {
                        currentStep++;
                        double t = (double)currentStep / steps;
                        double ease = Math.Pow(t, 2);

                        int currentX = (int)Math.Round(startX + (endX - startX) * ease);
                        int currentY = (int)Math.Round(startY + (endY - startY) * ease);

                        SetWindowPos(hwnd, IntPtr.Zero, currentX, currentY, 0, 0,
                            SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                        DwmFlush();
                    }
                });
            }
        }
        catch { }

        try
        {
            if (_dimWin != null)
            {
                _dimWin.Close();
                _dimWin = null;
            }
        }
        catch { }

        // Restore normal taskbar icon before hiding
        if (TaskTile.Services.SettingsService.Current.UseGrayscaleTaskbarIcon && !string.IsNullOrEmpty(_groupId))
        {
            var grp = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
            if (grp != null) TaskTile.Services.TaskbarService.RestoreNormalTaskbarIcon(grp);
        }

        if (TaskTile.Services.SettingsService.Current.StartPopupsInBackground)
        {
            try { AppWindow.Hide(); } catch { }
            _isClosing = false;
            // Trim working set after hiding to keep memory footprint low
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    System.Threading.Thread.Sleep(500);
                    var proc = System.Diagnostics.Process.GetCurrentProcess();
                    NativeMethods.SetProcessWorkingSetSize(proc.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
            });
        }
        else
        {
            try { this.Close(); } catch { }
        }
    }

    // ─── Events ──────────────────────────────────────────────────────────────
    void OnActivated(object s, WindowActivatedEventArgs e)
    {
        if (!AppWindow.IsVisible) return;
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            _activated = true;
        }
        else if (_activated && !_keepOpen && !_isInEditMode)
        {
            _ = CloseWithFadeAsync();
        }
    }

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

    private static void LaunchTarget(string? targetPath, string? arguments = null, bool runAsAdmin = false)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return;
        try
        {
            var psi = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                psi.Arguments = arguments;
            }
            if (runAsAdmin)
            {
                psi.Verb = "runas";
            }
            try
            {
                if (System.IO.File.Exists(targetPath) || System.IO.Directory.Exists(targetPath))
                {
                    var dir = System.IO.Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                    {
                        psi.WorkingDirectory = dir;
                    }
                }
            }
            catch { }

            var proc = Process.Start(psi);
            if (proc != null)
            {
                TaskTile.Services.AppRunningService.RegisterLaunchedProcess(targetPath, proc.Id);
            }
        }
        catch { }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OPENFILENAME ofn);

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_EXPLORER      = 0x00080000;
    private const int OFN_NOCHANGEDIR   = 0x00000008;

    private static string? PickExecutableWin32(IntPtr hwnd)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = hwnd;
        ofn.lpstrFilter = "Applications & Shortcuts (*.exe;*.lnk;*.url;*.bat;*.cmd;*.msi)\0*.exe;*.lnk;*.url;*.bat;*.cmd;*.msi\0All Files (*.*)\0*.*\0\0";
        ofn.lpstrFile = new string(new char[2048]);
        ofn.nMaxFile = 2048;
        ofn.lpstrTitle = "Select an Application or Shortcut";
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_EXPLORER | OFN_NOCHANGEDIR;

        if (GetOpenFileName(ofn))
        {
            return ofn.lpstrFile.TrimEnd('\0');
        }
        return null;
    }

    private void ClassicR_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is Microsoft.UI.Xaml.Controls.Primitives.SelectorItem container && args.Item is AppEntryViewModel vm)
        {
            if (sender == _classicR || sender.Parent == _classicPagesPanel || sender == _modernR || sender == _cardR)
            {
                double pad = vm.TileSpacing / 2.0;
                container.Margin = new Thickness(pad);
                container.Padding = new Thickness(0);
            }

            if (container is GridViewItem)
            {
                VariableSizedWrapGrid.SetColumnSpan(container, vm.ColSpan);
                VariableSizedWrapGrid.SetRowSpan(container, vm.RowSpan);
            }

            container.AllowDrop = true;
            container.DragOver -= Container_DragOver;
            container.DragOver += Container_DragOver;
            container.Drop -= Container_Drop;
            container.Drop += Container_Drop;

            // Intentional, smooth, quick tile hover animation
            container.PointerEntered -= TileContainer_PointerEntered;
            container.PointerEntered += TileContainer_PointerEntered;
            container.PointerExited -= TileContainer_PointerExited;
            container.PointerExited += TileContainer_PointerExited;
            container.PointerCanceled -= TileContainer_PointerExited;
            container.PointerCanceled += TileContainer_PointerExited;
            container.PointerCaptureLost -= TileContainer_PointerExited;
            container.PointerCaptureLost += TileContainer_PointerExited;
        }
    }

    private void TileContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_disableAnimation || _isInEditMode) return;
        if (sender is not FrameworkElement fe) return;
        Canvas.SetZIndex(fe, 100);

        try
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(fe);
            var comp = visual.Compositor;
            float w = (float)fe.ActualWidth;
            float h = (float)fe.ActualHeight;
            if (w > 0 && h > 0)
            {
                visual.CenterPoint = new System.Numerics.Vector3(w / 2f, h / 2f, 0f);
            }

            var scaleAnim = comp.CreateVector3KeyFrameAnimation();
            var ease = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.1f, 0.9f), new System.Numerics.Vector2(0.2f, 1.0f));
            scaleAnim.InsertKeyFrame(1.0f, new System.Numerics.Vector3(1.055f, 1.055f, 1.0f), ease);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(85); // Smooth & quick (85ms)
            visual.StartAnimation("Scale", scaleAnim);
        }
        catch { }
    }

    private void TileContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        Canvas.SetZIndex(fe, 0);

        try
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(fe);
            var comp = visual.Compositor;
            float w = (float)fe.ActualWidth;
            float h = (float)fe.ActualHeight;
            if (w > 0 && h > 0)
            {
                visual.CenterPoint = new System.Numerics.Vector3(w / 2f, h / 2f, 0f);
            }

            var scaleAnim = comp.CreateVector3KeyFrameAnimation();
            var ease = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.3f, 0.0f), new System.Numerics.Vector2(0.2f, 1.0f));
            scaleAnim.InsertKeyFrame(1.0f, new System.Numerics.Vector3(1.00f, 1.00f, 1.0f), ease);
            scaleAnim.Duration = TimeSpan.FromMilliseconds(110);
            visual.StartAnimation("Scale", scaleAnim);
        }
        catch { }
    }

    private TransitionCollection? CreateEntranceTransitions()
    {
        if (_disableAnimation || !TaskTile.Services.SettingsService.Current.EnableAppEntranceAnimation)
        {
            return null;
        }

        var trans = new TransitionCollection();
        trans.Add(new EntranceThemeTransition
        {
            IsStaggeringEnabled = true,
            FromVerticalOffset = 24
        });
        return trans;
    }

    private void Container_DragOver(object sender, DragEventArgs e)
    {
        if (!_isInEditMode) return;
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.IsCaptionVisible = false;
        e.DragUIOverride.IsGlyphVisible = false;
    }

    private void Container_Drop(object sender, DragEventArgs e)
    {
        if (!_isInEditMode) return;
        if (e.DataView.Properties.TryGetValue("DraggedApp", out var obj) && obj is AppEntryViewModel dragged)
        {
            AppEntryViewModel? target = null;
            if (sender is FrameworkElement fe && fe.DataContext is AppEntryViewModel vm)
            {
                target = vm;
            }
            if (target != null && target != dragged)
            {
                int oldIdx = _appsList.IndexOf(dragged);
                int newIdx = _appsList.IndexOf(target);
                if (oldIdx >= 0 && newIdx >= 0)
                {
                    _appsList.Move(oldIdx, newIdx);
                    SaveCurrentGroupApps();
                    _classicR.InvalidateMeasure();
                }
                e.Handled = true;
            }
        }
    }

    private void ClassicR_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!_isInEditMode)
        {
            e.Cancel = true;
            return;
        }
        var item = e.Items.FirstOrDefault() as AppEntryViewModel;
        if (item != null)
        {
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.Data.Properties["DraggedApp"] = item;
        }
    }

    private void ClassicR_DragOver(object sender, DragEventArgs e)
    {
        if (!_isInEditMode) return;
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.IsCaptionVisible = false;
        e.DragUIOverride.IsGlyphVisible = false;
    }

    private void ClassicR_Drop(object sender, DragEventArgs e)
    {
        if (!_isInEditMode) return;
        if (e.DataView.Properties.TryGetValue("DraggedApp", out var obj) && obj is AppEntryViewModel dragged)
        {
            int oldIdx = _appsList.IndexOf(dragged);
            if (oldIdx < 0) return;

            Windows.Foundation.Point pt = e.GetPosition(_classicR);
            var elements = VisualTreeHelper.FindElementsInHostCoordinates(pt, _classicR);
            AppEntryViewModel? target = null;
            foreach (var el in elements)
            {
                if (el is FrameworkElement fe && fe.DataContext is AppEntryViewModel vm && vm != dragged)
                {
                    target = vm;
                    break;
                }
            }

            int newIdx = target != null ? _appsList.IndexOf(target) : _appsList.Count - 1;
            if (newIdx >= 0 && newIdx != oldIdx)
            {
                _appsList.Move(oldIdx, newIdx);
                SaveCurrentGroupApps();
                _classicR.InvalidateMeasure();
            }
        }
    }

    void App_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_isInEditMode) return;
        if (e.ClickedItem is AppEntryViewModel vm && !string.IsNullOrEmpty(vm.ExePath))
        {
            HandleAppClick(vm);
        }
        _ = CloseWithFadeAsync();
    }

    private void HandleAppClick(AppEntryViewModel vm)
    {
        bool isShift = TaskTile.Services.AppRunningService.IsShiftPressed();
        if (!isShift && vm.IsRunning)
        {
            if (TaskTile.Services.AppRunningService.TryActivateRunningInstance(vm.ExePath))
            {
                return;
            }
        }
        LaunchTarget(vm.ExePath, vm.Arguments, vm.RunAsAdmin);
    }

    void Open_Click(object s, RoutedEventArgs e)
    {
        if (_isInEditMode) return;
        if (s is MenuFlyoutItem i && i.Tag is string p && !string.IsNullOrEmpty(p))
        {
            var vm = _appsList.FirstOrDefault(a => a.ExePath == p);
            bool isShift = TaskTile.Services.AppRunningService.IsShiftPressed();
            if (!isShift && TaskTile.Services.AppRunningService.IsAppRunning(p))
            {
                if (TaskTile.Services.AppRunningService.TryActivateRunningInstance(p))
                {
                    _ = CloseWithFadeAsync();
                    return;
                }
            }
            LaunchTarget(p, vm?.Arguments, vm?.RunAsAdmin ?? false);
        }
        _ = CloseWithFadeAsync();
    }

    private void LaunchOptions_Click(object sender, RoutedEventArgs e)
    {
        AppEntryViewModel? vm = null;
        if (sender is MenuFlyoutItem item)
        {
            if (item.Tag is AppEntryViewModel m) vm = m;
            else if (item.Tag is string p) vm = _appsList.FirstOrDefault(a => a.ExePath == p);
        }
        if (vm == null) return;
        ShowLaunchOptionsDialog(vm);
    }

    private void ShowLaunchOptionsDialog(AppEntryViewModel vm)
    {
        _savedKeepOpen = _keepOpen;
        _keepOpen = true;

        var parentRect = new Windows.Graphics.RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        var win = new LaunchOptionsWindow(parentRect, vm.Name, vm.Arguments, vm.RunAsAdmin,
            onSaved: (newArgs, newAdmin) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    vm.Arguments = newArgs;
                    vm.RunAsAdmin = newAdmin;
                    SaveCurrentGroupApps();
                });
            },
            onClosed: () =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _keepOpen = _savedKeepOpen;
                });
            });
        win.Activate();
    }

    private void Title_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(sender as UIElement).Properties;
        if (props.IsLeftButtonPressed)
        {
            BeginInlineRename();
        }
    }

    private void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        BeginInlineRename();
    }

    private void EditGroupParent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (App.MainWindowInstance != null)
            {
                App.MainWindowInstance.OpenGroupParentSettings(_groupId);
            }
        }
        catch { }
    }

    public void BeginInlineRename()
    {
        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group == null || _titleEditBox == null || _title == null) return;

        _savedKeepOpen = _keepOpen;
        _keepOpen = true;

        _title.Visibility = Visibility.Collapsed;
        _titleEditBox.Visibility = Visibility.Visible;
        _titleEditBox.Text = group.Name;
        _titleEditBox.SelectAll();
        _titleEditBox.Focus(FocusState.Programmatic);
    }

    private void CommitInlineRename()
    {
        if (_titleEditBox == null || _titleEditBox.Visibility != Visibility.Visible) return;

        var newName = _titleEditBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
            if (group != null)
            {
                group.Name = newName;
                if (_title != null) _title.Text = group.Name;
                if (_cardFooterName != null) _cardFooterName.Text = group.Name;
                TaskTile.Services.GroupService.Instance.Save();
                if (group.IsPinned) TaskTile.Services.TaskbarService.UpdateGroupShortcut(group);
            }
        }

        _titleEditBox.Visibility = Visibility.Collapsed;
        if (_title != null) _title.Visibility = Visibility.Visible;
        _keepOpen = _savedKeepOpen;
    }

    private void CancelInlineRename()
    {
        if (_titleEditBox == null) return;
        _titleEditBox.Visibility = Visibility.Collapsed;
        if (_title != null) _title.Visibility = Visibility.Visible;
        _keepOpen = _savedKeepOpen;
    }

    private void TitleEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitInlineRename();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelInlineRename();
            e.Handled = true;
        }
    }

    private void TitleEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitInlineRename();
    }

    private void OnNotificationCountsChanged()
    {
        DispatcherQueue?.TryEnqueue(() => RefreshNotificationCounts());
    }

    public void RefreshNotificationCounts()
    {
        if (_appsList == null || _appsList.Count == 0) return;
        foreach (var app in _appsList)
        {
            app.NotificationCount = TaskTile.Services.NotificationBadgeService.GetNotificationCount(app.Name, app.ExePath);
        }
    }

    void EditGroup_Click(object s, RoutedEventArgs e)
    {
        EnterEditMode();
    }

    public void EnterEditMode()
    {
        _isInEditMode = true;
        _savedKeepOpen = _keepOpen;
        _keepOpen = true;
        _editModeHeaderPanel.Visibility = Visibility.Visible;
        if (_headerEditBtn != null) _headerEditBtn.Visibility = Visibility.Collapsed;

        foreach (var app in _appsList)
        {
            app.EditModeVisibility = Visibility.Visible;
        }

        // Display all apps in a single scrollable grid for editing/reordering
        if (_classicPagesPanel.Children.Count > 1 || !_classicPagesPanel.Children.Contains(_classicR))
        {
            _classicPagesPanel.Children.Clear();
            _classicPagesPanel.Children.Add(_classicR);
        }
        _classicR.ItemsSource = _appsList;
        _classicR.Visibility = Visibility.Visible;
        _pageDots.Visibility = Visibility.Collapsed;
        _classicSV.VerticalScrollMode = ScrollMode.Auto;
        _classicSV.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _classicSV.ManipulationMode = ManipulationModes.System;

        _classicR.SelectionMode = ListViewSelectionMode.Single;
        _classicR.CanReorderItems = true;
        _classicR.CanDragItems = true;
        _classicR.AllowDrop = true;
        _classicR.IsItemClickEnabled = false;

        _modernR.SelectionMode = ListViewSelectionMode.Single;
        _modernR.CanReorderItems = true;
        _modernR.CanDragItems = true;
        _modernR.AllowDrop = true;
        _modernR.IsItemClickEnabled = false;

        _cardR.SelectionMode = ListViewSelectionMode.Single;
        _cardR.CanReorderItems = true;
        _cardR.CanDragItems = true;
        _cardR.AllowDrop = true;
        _cardR.IsItemClickEnabled = false;

        UpdateWindowSizeForEdit();
        StartEditWobble();
    }

    void ExitEditMode()
    {
        _isInEditMode = false;
        _keepOpen = _savedKeepOpen;
        StopEditWobble();
        _root.Opacity = 1.0;
        _editModeHeaderPanel.Visibility = Visibility.Collapsed;
        if (_headerEditBtn != null) _headerEditBtn.Visibility = Visibility.Collapsed;

        foreach (var app in _appsList)
        {
            app.EditModeVisibility = Visibility.Collapsed;
        }

        _classicSV.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _classicSV.VerticalScrollMode = ScrollMode.Disabled;
        _classicSV.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _classicSV.HorizontalScrollMode = ScrollMode.Disabled;
        _classicSV.ManipulationMode = ManipulationModes.None;
        _classicPage = 0;
        _classicSV.ChangeView(null, 0, null, true);
        if (_classicPagesPanel.RenderTransform is TranslateTransform ttEdit) ttEdit.Y = 0;

        _classicR.SelectionMode = ListViewSelectionMode.None;
        _classicR.CanReorderItems = false;
        _classicR.CanDragItems = false;
        _classicR.AllowDrop = false;
        _classicR.IsItemClickEnabled = true;

        _modernR.SelectionMode = ListViewSelectionMode.None;
        _modernR.CanReorderItems = false;
        _modernR.CanDragItems = false;
        _modernR.AllowDrop = false;
        _modernR.IsItemClickEnabled = true;

        _cardR.SelectionMode = ListViewSelectionMode.None;
        _cardR.CanReorderItems = false;
        _cardR.CanDragItems = false;
        _cardR.AllowDrop = false;
        _cardR.IsItemClickEnabled = true;

        SaveCurrentGroupApps();
        LoadAndPosition();
    }

    void DoneEditBtn_Click(object s, RoutedEventArgs e)
    {
        ExitEditMode();
    }

    private void AddSpecificFile_Click(object sender, RoutedEventArgs e)
    {
        AddAppFromFilePicker();
    }

    private void AddInstalledApp_Click(object sender, RoutedEventArgs e)
    {
        ShowInstalledAppsPicker();
    }

    private void ShowInstalledAppsPicker()
    {
        try
        {
            var parentRect = new Windows.Graphics.RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
            var pickerWin = new InstalledAppsPickerWindow(
                parentRect,
                onAppSelected: (entry) =>
                {
                    DispatcherQueue.TryEnqueue(() => AddAppToCurrentGroup(entry.Name, entry.ExePath));
                },
                onBrowseFile: () =>
                {
                    DispatcherQueue.TryEnqueue(() => AddAppFromFilePicker());
                });
            pickerWin.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening installed apps picker: {ex.Message}");
            AddAppFromFilePicker();
        }
    }

    private void AddAppToCurrentGroup(string friendlyName, string selectedFilePath)
    {
        if (string.IsNullOrEmpty(selectedFilePath)) return;

        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group != null)
        {
            var newEntry = new TaskTile.Models.AppEntry
            {
                Name = friendlyName,
                ExePath = selectedFilePath,
                TileSize = 1
            };
            group.Apps.Add(newEntry);
            TaskTile.Services.GroupService.Instance.Save();
            if (group.IsPinned) TaskTile.Services.TaskbarService.UpdateGroupShortcut(group);

            var vm = new AppEntryViewModel
            {
                Name = friendlyName,
                ExePath = selectedFilePath,
                IconImage = null,
                NormalVisibility = Visibility.Visible,
                MonotoneVisibility = Visibility.Collapsed,
                LabelVisibility = Visibility.Visible,
                UwpBackground = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                TileCornerRadius = new CornerRadius(10),
                EditModeVisibility = Visibility.Visible,
                TileSize = 1
            };
            _appsList.Add(vm);

            var dispatcher = this.DispatcherQueue;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var extracted = ExtractIconSync(selectedFilePath, "", false, false, Windows.UI.Color.FromArgb(255, 0, 120, 215));
                if (extracted != null)
                {
                    dispatcher?.TryEnqueue(() => vm.IconImage = new BitmapImage(new Uri(extracted)));
                }
            });

            UpdateWindowSizeForEdit();
        }
    }

    private async void AddAppFromFilePicker()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            string? selectedFilePath = null;

            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add(".exe");
                picker.FileTypeFilter.Add(".lnk");
                picker.FileTypeFilter.Add(".url");
                picker.FileTypeFilter.Add(".bat");
                picker.FileTypeFilter.Add(".cmd");
                picker.FileTypeFilter.Add(".msi");
                picker.FileTypeFilter.Add("*");

                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var file = await picker.PickSingleFileAsync();
                if (file != null) selectedFilePath = file.Path;
            }
            catch
            {
                selectedFilePath = null;
            }

            if (string.IsNullOrEmpty(selectedFilePath))
            {
                selectedFilePath = await System.Threading.Tasks.Task.Run(() => PickExecutableWin32(hwnd));
            }

            if (string.IsNullOrEmpty(selectedFilePath) || !System.IO.File.Exists(selectedFilePath)) return;

            string friendlyName = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(selectedFilePath);
                    if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
                    if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                }
                catch { }
                return System.IO.Path.GetFileNameWithoutExtension(selectedFilePath);
            });

            AddAppToCurrentGroup(friendlyName, selectedFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding app: {ex.Message}");
        }
    }

    private void AddAppBtn_Click(object sender, RoutedEventArgs e)
    {
        AddAppFromFilePicker();
    }

    private async void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AppEntryViewModel vm)
        {
            FrameworkElement? tile = btn.Parent as FrameworkElement;
            DependencyObject? curr = btn;
            while (curr != null && curr is not Microsoft.UI.Xaml.Controls.Primitives.SelectorItem)
            {
                curr = VisualTreeHelper.GetParent(curr);
            }
            if (curr is FrameworkElement selectorItem) tile = selectorItem;

            if (tile != null)
            {
                await AnimateScreamAndPoofAsync(tile);
            }

            var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
            if (group != null)
            {
                var match = group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath && a.Name == vm.Name)
                            ?? group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath);
                if (match != null)
                {
                    group.Apps.Remove(match);
                    TaskTile.Services.GroupService.Instance.Save();
                    if (group.IsPinned) TaskTile.Services.TaskbarService.UpdateGroupShortcut(group);
                }
            }
            _appsList.Remove(vm);
            UpdateWindowSizeForEdit();
        }
    }

    private void CycleSizeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AppEntryViewModel vm)
        {
            int nextSize = (vm.TileSize % 4) + 1;
            ApplyTileSize(vm, nextSize);
        }
    }

    private double _dragAccumX = 0;
    private double _dragAccumY = 0;

    private void SizeBadge_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AppEntryViewModel vm)
        {
            _dragAccumX += e.Delta.Translation.X;
            _dragAccumY += e.Delta.Translation.Y;

            if (_dragAccumX > 24 || _dragAccumY > 24)
            {
                _dragAccumX = 0;
                _dragAccumY = 0;
                int next = (vm.TileSize % 4) + 1;
                ApplyTileSize(vm, next);
            }
            else if (_dragAccumX < -24 || _dragAccumY < -24)
            {
                _dragAccumX = 0;
                _dragAccumY = 0;
                int prev = vm.TileSize - 1;
                if (prev < 1) prev = 4;
                ApplyTileSize(vm, prev);
            }
        }
    }

    private void ApplyTileSize(AppEntryViewModel vm, int newSize)
    {
        vm.TileSize = newSize;

        if (_classicR.ContainerFromItem(vm) is GridViewItem container)
        {
            VariableSizedWrapGrid.SetColumnSpan(container, vm.ColSpan);
            VariableSizedWrapGrid.SetRowSpan(container, vm.RowSpan);
        }
        _classicR.InvalidateMeasure();
        _classicR.InvalidateArrange();

        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group != null)
        {
            var match = group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath && a.Name == vm.Name)
                        ?? group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath);
            if (match != null)
            {
                match.TileSize = vm.TileSize;
                TaskTile.Services.GroupService.Instance.Save();
            }
        }
        UpdateWindowSizeForEdit();
    }

    private void UpdateWindowSizeForEdit()
    {
        // Maintain layout stability in edit mode - do not arbitrarily enlarge client dimensions
    }

    private void RecalculateWindowSizeAfterEdit()
    {
        try
        {
            LoadAndPosition();
        }
        catch { }
    }

    private void SaveCurrentGroupApps()
    {
        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group != null)
        {
            var updated = new List<TaskTile.Models.AppEntry>();
            foreach (var vm in _appsList)
            {
                var existing = group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath && a.Name == vm.Name)
                            ?? group.Apps.FirstOrDefault(a => a.ExePath == vm.ExePath);
                if (existing != null)
                {
                    existing.TileSize = vm.TileSize;
                    existing.Arguments = vm.Arguments;
                    existing.RunAsAdmin = vm.RunAsAdmin;
                    updated.Add(existing);
                }
                else
                {
                    updated.Add(new TaskTile.Models.AppEntry
                    {
                        Name = vm.Name,
                        ExePath = vm.ExePath,
                        TileSize = vm.TileSize,
                        Arguments = vm.Arguments,
                        RunAsAdmin = vm.RunAsAdmin
                    });
                }
            }
            group.Apps = updated;
            TaskTile.Services.GroupService.Instance.Save();
            if (group.IsPinned) TaskTile.Services.TaskbarService.UpdateGroupShortcut(group);
        }
    }

    void LaunchAll_Click(object s, RoutedEventArgs e)
    {
        if (_isInEditMode) return;
        var group = TaskTile.Services.GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == _groupId);
        if (group?.Apps != null)
        {
            foreach (var app in group.Apps)
            {
                var exe = app.ExePath;
                if (!string.IsNullOrEmpty(exe))
                {
                    LaunchTarget(exe, app.Arguments, app.RunAsAdmin);
                }
            }
        }
        _ = CloseWithFadeAsync();
    }

    void RunAdmin_Click(object s, RoutedEventArgs e)
    {
        if (_isInEditMode) return;
        if (s is MenuFlyoutItem i && i.Tag is string p && !string.IsNullOrEmpty(p))
        {
            var vm = _appsList.FirstOrDefault(a => a.ExePath == p);
            LaunchTarget(p, vm?.Arguments, runAsAdmin: true);
        }
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

        double targetY = -page * _classicPageHeight;

        if (_classicPagesPanel.RenderTransform is TranslateTransform tt)
        {
            if (_disableAnimation)
            {
                tt.Y = targetY;
            }
            else
            {
                var sb = new Storyboard();
                var anim = new DoubleAnimation
                {
                    To = targetY,
                    Duration = new Duration(TaskTile.Helpers.AnimationHelper.ScaleDuration(TimeSpan.FromMilliseconds(220))),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(anim, tt);
                Storyboard.SetTargetProperty(anim, "Y");
                sb.Children.Add(anim);
                sb.Begin();
            }
        }
        else
        {
            _classicSV.ChangeView(null, page * _classicPageHeight, null, _disableAnimation);
        }

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
// ─── Edit Mode Jiggle & Scared Squirm Animations ─────────────────────
    private DispatcherTimer? _editWobbleTimer;
    private int _editWobbleFrame = 0;
    private readonly System.Collections.Generic.Dictionary<FrameworkElement, DispatcherTimer> _scaredTimers = new();

    private void StartEditWobble()
    {
        StopEditWobble();
        _ = RunInitialEditBounceAsync();

        _editWobbleFrame = 0;
        _editWobbleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(24) };
        _editWobbleTimer.Tick += (s, e) =>
        {
            if (!_isInEditMode)
            {
                StopEditWobble();
                return;
            }

            _editWobbleFrame++;
            var containers = GetActiveItemsControls();
            int idx = 0;
            foreach (var ctrl in containers)
            {
                if (ctrl.ItemsSource is System.Collections.IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (ctrl.ContainerFromIndex(i) is FrameworkElement container)
                        {
                            if (!_scaredTimers.ContainsKey(container))
                            {
                                double phase = (_editWobbleFrame + (idx % 4) * 5) * 0.42;
                                double angle = Math.Sin(phase) * 1.6;
                                double offsetY = Math.Cos(phase) * 0.7;

                                if (container.RenderTransform is not CompositeTransform ct)
                                {
                                    ct = new CompositeTransform();
                                    container.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                                    container.RenderTransform = ct;
                                }
                                ct.Rotation = angle;
                                ct.TranslateY = offsetY;
                            }
                        }
                        idx++;
                    }
                }
            }
        };
        _editWobbleTimer.Start();
    }

    private async System.Threading.Tasks.Task RunInitialEditBounceAsync()
    {
        try
        {
            var containers = GetActiveItemsControls();
            for (int f = 1; f <= 6; f++)
            {
                double dy = -Math.Sin(f / 6.0 * Math.PI) * 5.0;
                foreach (var ctrl in containers)
                {
                    if (ctrl.ItemsSource is System.Collections.IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (ctrl.ContainerFromIndex(i) is FrameworkElement c)
                            {
                                if (c.RenderTransform is not CompositeTransform ct)
                                {
                                    ct = new CompositeTransform();
                                    c.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                                    c.RenderTransform = ct;
                                }
                                ct.TranslateY = dy;
                            }
                        }
                    }
                }
                await System.Threading.Tasks.Task.Delay(20);
            }
        }
        catch { }
    }

    private void StopEditWobble()
    {
        if (_editWobbleTimer != null)
        {
            _editWobbleTimer.Stop();
            _editWobbleTimer = null;
        }

        foreach (var timer in _scaredTimers.Values)
        {
            try { timer.Stop(); } catch { }
        }
        _scaredTimers.Clear();

        var containers = GetActiveItemsControls();
        foreach (var ctrl in containers)
        {
            if (ctrl.ItemsSource is System.Collections.IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (ctrl.ContainerFromIndex(i) is FrameworkElement container)
                    {
                        if (container.RenderTransform is CompositeTransform ct)
                        {
                            ct.Rotation = 0;
                            ct.TranslateX = 0;
                            ct.TranslateY = 0;
                            ct.ScaleX = 1;
                            ct.ScaleY = 1;
                        }
                    }
                }
            }
        }
    }

    private System.Collections.Generic.List<ItemsControl> GetActiveItemsControls()
    {
        var list = new System.Collections.Generic.List<ItemsControl>();
        if (_classicR != null && _classicR.Visibility == Visibility.Visible) list.Add(_classicR);
        if (_modernR != null && _modernR.Visibility == Visibility.Visible) list.Add(_modernR);
        if (_cardR != null && _cardR.Visibility == Visibility.Visible) list.Add(_cardR);
        if (_compactR != null && _compactR.Visibility == Visibility.Visible) list.Add(_compactR);
        if (_compactRVertical != null && _compactRVertical.Visibility == Visibility.Visible) list.Add(_compactRVertical);
        if (_listR != null && _listR.Visibility == Visibility.Visible) list.Add(_listR);
        if (_classicPagesPanel != null)
        {
            foreach (var child in _classicPagesPanel.Children)
            {
                if (child is ItemsControl ic && !list.Contains(ic)) list.Add(ic);
            }
        }
        return list;
    }

    private readonly System.Collections.Generic.Dictionary<FrameworkElement, long> _worryStartTimes = new();

    private void RemoveAppBtn_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement btn)
        {
            FrameworkElement? target = btn.Parent as FrameworkElement;
            DependencyObject? curr = btn;
            while (curr != null && curr is not Microsoft.UI.Xaml.Controls.Primitives.SelectorItem)
            {
                curr = VisualTreeHelper.GetParent(curr);
            }
            if (curr is FrameworkElement selectorItem) target = selectorItem;

            if (target != null)
            {
                StartProgressiveWorryAnimation(target);
            }
        }
    }

    private void RemoveAppBtn_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement btn)
        {
            FrameworkElement? target = btn.Parent as FrameworkElement;
            DependencyObject? curr = btn;
            while (curr != null && curr is not Microsoft.UI.Xaml.Controls.Primitives.SelectorItem)
            {
                curr = VisualTreeHelper.GetParent(curr);
            }
            if (curr is FrameworkElement selectorItem) target = selectorItem;

            if (target != null)
            {
                StopProgressiveWorryAnimation(target);
            }
        }
    }

    private void StartProgressiveWorryAnimation(FrameworkElement tile)
    {
        StopProgressiveWorryAnimation(tile);

        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s, e) =>
        {
            if (!_isInEditMode)
            {
                StopProgressiveWorryAnimation(tile);
                return;
            }

            double elapsedMs = (double)(System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) / System.Diagnostics.Stopwatch.Frequency * 1000.0;

            if (tile.RenderTransform is not CompositeTransform ct)
            {
                ct = new CompositeTransform();
                tile.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                tile.RenderTransform = ct;
            }

            if (elapsedMs < 400)
            {
                // Stage 1: Starts slow thinking nothing's happening (mild micro-twitch)
                double slowAngle = Math.Sin(elapsedMs * 0.015) * 0.5;
                ct.Rotation = slowAngle;
                ct.ScaleX = 1.0;
                ct.ScaleY = 1.0;
                ct.TranslateX = 0;
                ct.TranslateY = 0;
            }
            else
            {
                // Stage 2: Slowly gets worried (intensity ramps up from 0 to 1 over next 900ms)
                double worryRamp = Math.Clamp((elapsedMs - 400.0) / 900.0, 0.0, 1.0);
                double t = elapsedMs * 0.025;

                double squirmAmp = 0.02 + 0.16 * worryRamp;
                double squirm = Math.Sin(t * 1.5) * squirmAmp;
                ct.ScaleX = 1.0 - squirm;
                ct.ScaleY = 1.0 + squirm * 1.3;

                double trembleAmp = 0.5 + 4.5 * worryRamp;
                double rotAmp = 0.8 + 5.5 * worryRamp;
                ct.TranslateX = (Random.Shared.NextDouble() - 0.5) * trembleAmp;
                ct.TranslateY = (Random.Shared.NextDouble() - 0.5) * (trembleAmp * 0.6);
                ct.Rotation = (Random.Shared.NextDouble() - 0.5) * rotAmp;
            }
        };

        _scaredTimers[tile] = timer;
        timer.Start();
    }

    private void StopProgressiveWorryAnimation(FrameworkElement tile)
    {
        if (_scaredTimers.TryGetValue(tile, out var timer))
        {
            timer.Stop();
            _scaredTimers.Remove(tile);
        }

        if (tile.RenderTransform is CompositeTransform ct)
        {
            ct.ScaleX = 1.0;
            ct.ScaleY = 1.0;
            ct.TranslateX = 0;
            ct.TranslateY = 0;
            ct.Rotation = 0;
        }
    }

    private async System.Threading.Tasks.Task AnimateScreamAndPoofAsync(FrameworkElement tile)
    {
        StopProgressiveWorryAnimation(tile);

        if (tile.RenderTransform is not CompositeTransform ct)
        {
            ct = new CompositeTransform();
            tile.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            tile.RenderTransform = ct;
        }

        // 1. SCREAM! (Stretches tall in shock and shakes frantically for 160ms)
        for (int i = 0; i < 10; i++)
        {
            ct.ScaleX = 0.74;
            ct.ScaleY = 1.42;
            ct.TranslateX = (Random.Shared.NextDouble() - 0.5) * 8.0;
            ct.TranslateY = (Random.Shared.NextDouble() - 0.5) * 6.0;
            ct.Rotation = (Random.Shared.NextDouble() - 0.5) * 10.0;
            await System.Threading.Tasks.Task.Delay(16);
        }

        // 2. POOF! (Explodes into a million pieces - scales to 0 and vanishes)
        for (int i = 1; i <= 8; i++)
        {
            double s = Math.Max(0, 1.0 - (double)i / 8);
            ct.ScaleX = s * 1.5;
            ct.ScaleY = s * 1.5;
            tile.Opacity = s;
            await System.Threading.Tasks.Task.Delay(16);
        }

        tile.Opacity = 0;
    }
}

// ─── Infrastructure ───────────────────────────────────────────────────────────
public class CustomTemplate : Microsoft.UI.Xaml.IElementFactory
{
    readonly Func<UIElement> _f;
    public CustomTemplate(Func<UIElement> f) => _f = f;
    public UIElement GetElement(ElementFactoryGetArgs a) => _f();
    public void RecycleElement(ElementFactoryRecycleArgs a) { }
}
