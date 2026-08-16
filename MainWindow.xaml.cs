using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TaskTile.Services;
using TaskTile.Helpers;
using TaskTile.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System.Numerics;
using Windows.Graphics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI;
using System;
using System.Linq;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;

namespace TaskTile;

public sealed partial class MainWindow : Window
{
    private Compositor? _compositor;
    private Visual? _homeVisual;
    private Visual? _groupsVisual;
    private SpringVector3NaturalMotionAnimation? _springAnimation;
    private TaskTile.Popups.PopupWindow? _cachedPopupWindow;

    public MainWindow()
    {
        this.InitializeComponent();

        this.RootGrid.Loaded += async (s, e) => {
            SetupComposition();

            // Default selection and navigation
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                ContentFrame.Navigate(typeof(HomePage));
            }

            // Hook burger/settings – retry until found
            await TryHookNavigationJuice();
        };

        // Also re-hook after pane opens (template parts may not exist until then)
        NavView.PaneOpened += async (s, e) => await TryHookNavigationJuice();

        SettingsService.Load();
        
        App.MainWindowInstance = this;
        this.AppWindow.Closing += AppWindow_Closing;
        this.Activated += MainWindow_Activated;
        SetupWindow(); // Must be called before window is shown!
    }

    private bool _isInitialized = false;

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "Square44x44Logo.targetsize-256.png");
                if (System.IO.File.Exists(iconPath)) this.AppWindow.SetIcon(iconPath);
            } catch { }

            ApplySettings();
            SettingsService.SettingsChanged += ApplySettings;
            
            // Initialize Tray Icon using Native SystemTrayManager
            SystemTrayManager.Initialize(
                showCallback: () => {
                    this.AppWindow.Show();
                    this.Activate();
                    NavigateTo(typeof(TaskTile.Pages.HomePage));
                },
                exitCallback: () => Application.Current.Exit()
            );

            GroupService.Instance.Groups.CollectionChanged += (s, e) => {
                SystemTrayManager.RebuildMenu();
            };

            InitializeBackgroundPopup();
            StartPipeServer();
        }
    }

    private void StartPipeServer()
    {
        _ = Task.Run(async () => {
            while (true) {
                try {
                    using var server = new NamedPipeServerStream("TaskTilePipe", PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    using var reader = new StreamReader(server);
                    string? groupId = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(groupId)) {
                        DispatcherQueue.TryEnqueue(() => {
                            if (groupId == "SHOW_MAIN") {
                                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                                App.ShowWindow(hwnd, 9); // SW_RESTORE
                                App.SetForegroundWindow(hwnd);
                            } else {
                                ShowGroup(groupId);
                            }
                        });
                    }
                } catch {
                    await Task.Delay(1000); // Backoff on error
                }
            }
        });
    }

    public void InitializeBackgroundPopup()
    {
        if (SettingsService.Current.StartPopupsInBackground)
        {
            if (_cachedPopupWindow == null)
            {
                _cachedPopupWindow = new TaskTile.Popups.PopupWindow("");
                // We don't activate it, so it stays hidden but initialized in the background
            }
        }
        else
        {
            if (_cachedPopupWindow != null)
            {
                _cachedPopupWindow.Close();
                _cachedPopupWindow = null;
            }
        }
    }

    public void ShowGroup(string groupId)
    {
        if (SettingsService.Current.StartPopupsInBackground && _cachedPopupWindow != null)
        {
            _cachedPopupWindow.LoadGroup(groupId);
            _cachedPopupWindow.AppWindow.Show();
            _cachedPopupWindow.Activate();
            _cachedPopupWindow.PlayPopInAnimation();
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_cachedPopupWindow);
            TaskTile.NativeMethods.SetForegroundWindow(hwnd);
        }
        else
        {
            var popup = new TaskTile.Popups.PopupWindow(groupId);
            popup.Activate();
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(popup);
            TaskTile.NativeMethods.SetForegroundWindow(hwnd);
        }
    }

    public void ShowSettings()
    {
        this.AppWindow.Show(); 
        NavigateTo(typeof(TaskTile.Pages.SettingsPage));
        this.Activate();
    }

    // Removed old TrayMenu event handlers as we now use SystemTrayManager

    private void SetupComposition()
    {
        _compositor = ElementCompositionPreview.GetElementVisual(RootGrid).Compositor;
        
        if (HomeItem.Icon is FontIcon homeIcon)
        {
            _homeVisual = ElementCompositionPreview.GetElementVisual(homeIcon);
            _homeVisual.CenterPoint = new Vector3(10, 10, 0);
        }

        if (GroupsItem.Icon is FontIcon groupsIcon)
        {
            _groupsVisual = ElementCompositionPreview.GetElementVisual(groupsIcon);
            _groupsVisual.CenterPoint = new Vector3(10, 10, 0);
        }

        _springAnimation = _compositor.CreateSpringVector3Animation();
        _springAnimation.Target = "Scale";
        _springAnimation.DampingRatio = 0.5f;
        _springAnimation.Period = TimeSpan.FromSeconds(0.12);
    }

    private void SetupWindow()
    {
        this.ExtendsContentIntoTitleBar = true;
        var appWindow = this.AppWindow;
        if (appWindow.TitleBar != null)
        {
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
        appWindow.Resize(new SizeInt32(1100, 750));
        Center();
    }

    private void Center()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        AppWindow.Move(new PointInt32(
            (workArea.Width - AppWindow.Size.Width) / 2,
            (workArea.Height - AppWindow.Size.Height) / 2
        ));
    }

    public void ApplySettings()
    {
        var s = SettingsService.Current;
        bool isLightMode = s.Theme == 1 || (s.Theme == 0 && Application.Current.RequestedTheme == ApplicationTheme.Light);

        if (this.Content is FrameworkElement root)
        {
            // ── Theme ──────────────────────────────────────────────────────
            if (s.Theme == 1) root.RequestedTheme = ElementTheme.Light;
            else if (s.Theme == 2) root.RequestedTheme = ElementTheme.Dark;
            else root.RequestedTheme = ElementTheme.Default;

            if (s.BackdropStyle == 3 && !isLightMode)
            {
                var oledKeys = new string[] {
                    "LayerFillColorDefaultBrush", "LayerOnAcrylicFillColorDefaultBrush",
                    "SolidBackgroundFillColorBaseBrush", "SolidBackgroundFillColorBaseAltBrush",
                    "CardBackgroundFillColorDefaultBrush", "CardBackgroundFillColorSecondaryBrush",
                    "CardBackgroundFillColorTertiaryBrush",
                    "NavigationViewDefaultPaneBackground", "NavigationViewExpandedPaneBackground",
                    "NavigationViewContentBackground", "NavigationViewContentGridBorderBrush",
                    "AcrylicBackgroundFillColorBaseBrush", "AcrylicBackgroundFillColorDefaultBrush",
                    "AcrylicInAppFillColorDefaultBrush", "AcrylicInAppFillColorBaseBrush",
                    "SmokeFillColorDefaultBrush"
                };
                foreach (var k in oledKeys)
                {
                    root.Resources.Remove(k);
                    root.Resources.Add(k, new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)));
                }
            }
            else
            {
                string[] oledKeys = {
                    "ApplicationPageBackgroundThemeBrush", "SystemControlPageBackgroundChromeLowBrush",
                    "LayerFillColorDefaultBrush", "LayerFillColorAltBrush",
                    "LayerOnAcrylicFillColorDefaultBrush", "LayerOnMicaBaseAltFillColorDefaultBrush",
                    "LayerOnMicaBaseAltFillColorSecondaryBrush", "LayerOnMicaBaseAltFillColorTertiaryBrush",
                    "LayerOnMicaBaseAltFillColorTransparentBrush",
                    "CardBackgroundFillColorDefaultBrush", "CardBackgroundFillColorSecondaryBrush",
                    "CardStrokeColorDefaultBrush",
                    "SolidBackgroundFillColorBaseBrush", "SolidBackgroundFillColorBaseAltBrush",
                    "SolidBackgroundFillColorSecondaryBrush", "SolidBackgroundFillColorTertiaryBrush",
                    "SolidBackgroundFillColorQuarternaryBrush",
                    "ControlFillColorDefaultBrush", "ControlFillColorSecondaryBrush",
                    "ControlFillColorTertiaryBrush", "ControlFillColorQuarternaryBrush",
                    "ControlFillColorDisabledBrush", "ControlFillColorTransparentBrush",
                    "ControlFillColorInputActiveBrush",
                    "ControlStrokeColorDefaultBrush", "ControlStrokeColorSecondaryBrush",
                    "ControlStrokeColorOnAccentDefaultBrush",
                    "SubtleFillColorSecondaryBrush", "SubtleFillColorTertiaryBrush", "SubtleFillColorTransparentBrush",
                    "NavigationViewDefaultPaneBackground", "NavigationViewExpandedPaneBackground",
                    "NavigationViewContentBackground", "NavigationViewContentGridBorderBrush",
                    "AcrylicBackgroundFillColorBaseBrush", "AcrylicBackgroundFillColorDefaultBrush",
                    "AcrylicInAppFillColorDefaultBrush", "AcrylicInAppFillColorBaseBrush",
                    "SmokeFillColorDefaultBrush"
                };
                foreach (var k in oledKeys) root.Resources.Remove(k);

                if (root is Panel panel)
                    panel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }

        try
        {
            FrostedOverlay.Visibility = Visibility.Collapsed;

            if (s.BackdropStyle == 3)          // None (OLED / Solid White)
            {
                this.SystemBackdrop = null;
                if (App.MainWindowInstance?.Content is Panel rootPanel)
                {
                     rootPanel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        isLightMode ? Microsoft.UI.Colors.White : Windows.UI.Color.FromArgb(255, 0, 0, 0));
                }
            }
            else if (s.BackdropStyle == 1)          // Mica (Base)
            {
                this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.Base };
            }
            else if (s.BackdropStyle == 2)          // Mica Alt
            {
                this.SystemBackdrop = new MicaBackdrop() { Kind = MicaKind.BaseAlt };
            }
            else                                    // 0 = Acrylic (default)
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }

            // Update title bar button colors based on theme
            if (AppWindow.TitleBar != null)
            {
                var btnColor = isLightMode ? Windows.UI.Color.FromArgb(255,   0,   0,   0)
                                           : Windows.UI.Color.FromArgb(255, 255, 255, 255);
                var hoverBg  = isLightMode ? Windows.UI.Color.FromArgb( 20,   0,   0,   0)
                                           : Windows.UI.Color.FromArgb( 20, 255, 255, 255);
                var pressed  = isLightMode ? Windows.UI.Color.FromArgb( 40,   0,   0,   0)
                                           : Windows.UI.Color.FromArgb( 40, 255, 255, 255);
                AppWindow.TitleBar.ButtonForegroundColor         = btnColor;
                AppWindow.TitleBar.ButtonHoverBackgroundColor    = hoverBg;
                AppWindow.TitleBar.ButtonHoverForegroundColor    = btnColor;
                AppWindow.TitleBar.ButtonPressedBackgroundColor  = pressed;
                AppWindow.TitleBar.ButtonPressedForegroundColor  = btnColor;
                AppWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(120, btnColor.R, btnColor.G, btnColor.B);
            }
        } catch { }
    }


    public void NavigateTo(Type pageType)
    {
        ContentFrame.Navigate(pageType);
        
        // Find in main or footer
        var items = NavView.MenuItems.Concat(NavView.FooterMenuItems).OfType<NavigationViewItem>();
        var target = items.FirstOrDefault(i => i.Tag?.ToString() == pageType.Name.Replace("Page", ""));
        if (target != null) NavView.SelectedItem = target;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            Type? pageType = tag switch
            {
                "Home" => typeof(HomePage),
                "Groups" => typeof(GroupsPage),
                "Settings" => typeof(SettingsPage),
                _ => typeof(HomePage)
            };
            
            if (pageType != null)
                ContentFrame.Navigate(pageType);
        }
    }

    private void Item_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            // Animate only the icon visual, not the whole row
            if (tag == "Home" && _homeVisual != null) AnimateVisual(_homeVisual, true);
            else if (tag == "Groups" && _groupsVisual != null) AnimateVisual(_groupsVisual, true);
        }
    }

    private void Item_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "Home" && _homeVisual != null) AnimateVisual(_homeVisual, false);
            else if (tag == "Groups" && _groupsVisual != null) AnimateVisual(_groupsVisual, false);
        }
    }

    private void Item_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "Home" && _homeVisual != null) BounceVisual(_homeVisual);
            else if (tag == "Groups" && _groupsVisual != null) BounceVisual(_groupsVisual);
        }
    }

    // Tracks hooked elements so we don't double-attach
    private readonly System.Collections.Generic.HashSet<string> _hookedNames = new();

    private async System.Threading.Tasks.Task TryHookNavigationJuice(int retries = 5)
    {
        for (int attempt = 0; attempt < retries; attempt++)
        {
            if (WalkAndHook(NavView)) return;
            await System.Threading.Tasks.Task.Delay(120);
        }
    }

    private bool WalkAndHook(DependencyObject parent)
    {
        bool foundAll = false;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe &&
                (fe.Name == "TogglePaneButton" || fe.Name == "SettingsNavPaneItem") &&
                !_hookedNames.Contains(fe.Name))
            {
                _hookedNames.Add(fe.Name);
                foundAll = true;
                fe.AddHandler(UIElement.PointerEnteredEvent,  new PointerEventHandler((_, _) => AnimateDynamicIcon(fe, true)),  true);
                fe.AddHandler(UIElement.PointerExitedEvent,   new PointerEventHandler((_, _) => AnimateDynamicIcon(fe, false)), true);
                fe.AddHandler(UIElement.PointerPressedEvent,  new PointerEventHandler((_, _) => BounceDynamicIcon(fe)),        true);
            }
            WalkAndHook(child);
        }
        return foundAll;
    }

    // Animate an icon-level visual (squish on enter, spring on exit)
    private void AnimateVisual(Visual visual, bool enter)
    {
        if (_compositor == null) return;
        if (enter)
        {
            var anim = _compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(1.0f, new Vector3(1.2f, 0.8f, 1.0f));
            anim.Duration = TimeSpan.FromSeconds(0.15);
            visual.StartAnimation("Scale", anim);
        }
        else if (_springAnimation != null)
        {
            _springAnimation.FinalValue = new Vector3(1.0f, 1.0f, 1.0f);
            visual.StartAnimation("Scale", _springAnimation);
        }
    }

    private void BounceVisual(Visual visual)
    {
        if (_compositor == null || _springAnimation == null) return;
        visual.Scale = new Vector3(0.75f, 0.75f, 1.0f);
        _springAnimation.FinalValue = new Vector3(1.0f, 1.0f, 1.0f);
        visual.StartAnimation("Scale", _springAnimation);
    }

    // Animate a whole FrameworkElement (hamburger / settings button)
    private void AnimateDynamicIcon(FrameworkElement item, bool enter)
    {
        var visual = ElementCompositionPreview.GetElementVisual(item);
        if (_compositor == null) return;
        visual.CenterPoint = new Vector3((float)item.ActualWidth / 2, (float)item.ActualHeight / 2, 0);
        AnimateVisual(visual, enter);
    }

    private void BounceDynamicIcon(FrameworkElement item)
    {
        var visual = ElementCompositionPreview.GetElementVisual(item);
        if (_compositor == null || _springAnimation == null) return;
        visual.CenterPoint = new Vector3((float)item.ActualWidth / 2, (float)item.ActualHeight / 2, 0);
        BounceVisual(visual);
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (SettingsService.Current.EnableTrayIcon)
        {
            args.Cancel = true;
            this.AppWindow.Hide();
        }
        else
            App.MainWindowInstance = null;
    }
}

class CustomAcrylicBackdrop : Microsoft.UI.Xaml.Media.SystemBackdrop
{
    private Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController? _controller;
    private Windows.UI.Color _tintColor;
    private float _tintOpacity;

    public CustomAcrylicBackdrop(Windows.UI.Color tintColor, float tintOpacity)
    {
        _tintColor = tintColor;
        _tintOpacity = tintOpacity;
    }

    protected override void OnTargetConnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop connectedTarget, Microsoft.UI.Xaml.XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        _controller = new Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController();
        _controller.TintColor = _tintColor;
        _controller.TintOpacity = _tintOpacity;
        _controller.LuminosityOpacity = 0.8f;
        _controller.FallbackColor = _tintColor;
        _controller.AddSystemBackdropTarget(connectedTarget);
    }

    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        if (_controller != null)
        {
            _controller.RemoveSystemBackdropTarget(disconnectedTarget);
            _controller.Dispose();
            _controller = null;
        }
    }
}
