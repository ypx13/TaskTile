using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TaskTile.Services;
using TaskTile.Helpers;
using TaskTile.Pages;
using Microsoft.UI.Windowing;
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
    private TaskTile.Popups.PopupWindow? _cachedPopupWindow;

    public MainWindow()
    {
        this.InitializeComponent();

        this.RootGrid.Loaded += (s, e) => {
            // Default selection and navigation
            if (NavView.MenuItems.Count > 0)
            {
                NavView.SelectedItem = NavView.MenuItems[0];
                ContentFrame.Navigate(typeof(HomePage));
            }
        };

        SettingsService.Load();
        
        App.MainWindowInstance = this;
        this.AppWindow.Closing += AppWindow_Closing;
        this.Activated += MainWindow_Activated;
        RootGrid.KeyDown += RootGrid_KeyDown;
        SetupWindow(); // Must be called before window is shown!
    }

    private readonly System.Text.StringBuilder _secretBuffer = new();
    private DateTime _lastSecretTime = DateTime.MinValue;

    private void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if ((DateTime.Now - _lastSecretTime).TotalSeconds > 3)
            _secretBuffer.Clear();

        _lastSecretTime = DateTime.Now;

        char c = (char)0;
        var key = e.Key;
        if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
            c = (char)('a' + (key - Windows.System.VirtualKey.A));
        else if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
            c = (char)('0' + (key - Windows.System.VirtualKey.Number0));

        if (c != (char)0)
        {
            _secretBuffer.Append(c);
            if (_secretBuffer.Length > 20)
                _secretBuffer.Remove(0, _secretBuffer.Length - 20);

            string buf = _secretBuffer.ToString();
            if (buf.EndsWith("tasktile26") || buf.EndsWith("26tasktile"))
            {
                _secretBuffer.Clear();
                SettingsService.Current.PersistentDebugMode = true;
                SettingsService.Save();
                NavigateTo(typeof(TaskTile.Pages.SettingsPage));
                if (ContentFrame.Content is TaskTile.Pages.SettingsPage sp)
                {
                    sp.ShowDebugPanel();
                }
            }
        }
    }

    private bool _isInitialized = false;

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_isInitialized)
        {
            _isInitialized = true;
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "newicon.ico");
                if (System.IO.File.Exists(iconPath)) this.AppWindow.SetIcon(iconPath);

                var logoPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "newicon.png");
                if (System.IO.File.Exists(logoPath))
                {
                    AppLogoImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(logoPath));
                }
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

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            HotkeyService.Instance.Initialize(hwnd, this.DispatcherQueue);
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
                    string? msg = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(msg)) {
                        DispatcherQueue.TryEnqueue(() => {
                            if (msg == "SHOW_MAIN") {
                                AppWindow.Show();
                                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                                App.ShowWindow(hwnd, 9); // SW_RESTORE
                                App.SetForegroundWindow(hwnd);
                            } else if (msg.StartsWith("EDIT_GROUP:")) {
                                string gid = msg.Substring("EDIT_GROUP:".Length);
                                ShowGroup(gid, "edit");
                            } else if (msg.StartsWith("SETTINGS_GROUP:")) {
                                string gid = msg.Substring("SETTINGS_GROUP:".Length);
                                OpenGroupParentSettings(gid);
                            } else if (msg.StartsWith("RENAME_GROUP:")) {
                                string gid = msg.Substring("RENAME_GROUP:".Length);
                                ShowGroup(gid, "rename");
                            } else {
                                ShowGroup(msg);
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

    public void ShowGroup(string groupId, string? action = null)
    {
        var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id.ToString() == groupId);
        if (group != null && group.GroupType == Models.GroupType.Radial)
        {
            RadialMenuManager.Show(group);
            return;
        }
        if (SettingsService.Current.StartPopupsInBackground && _cachedPopupWindow != null)
        {
            _cachedPopupWindow.LoadGroup(groupId, action);
            _cachedPopupWindow.AppWindow.Show();
            _cachedPopupWindow.Activate();
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_cachedPopupWindow);
            TaskTile.NativeMethods.SetForegroundWindow(hwnd);
        }
        else
        {
            var popup = new TaskTile.Popups.PopupWindow(groupId, action);
            popup.Activate();
            
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(popup);
            TaskTile.NativeMethods.SetForegroundWindow(hwnd);
        }
    }

    public void OpenGroupParentSettings(string groupId)
    {
        if (!Guid.TryParse(groupId, out var gid)) return;
        this.AppWindow.Show();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        App.ShowWindow(hwnd, 9); // SW_RESTORE
        App.SetForegroundWindow(hwnd);
        NavigateTo(typeof(TaskTile.Pages.GroupsPage));
        this.Activate();

        if (ContentFrame.Content is TaskTile.Pages.GroupsPage gp)
        {
            gp.OpenGroupFullSettings(gid);
        }
        else
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (ContentFrame.Content is TaskTile.Pages.GroupsPage gp2)
                {
                    gp2.OpenGroupFullSettings(gid);
                }
            });
        }
    }

    public void ShowSettings()
    {
        this.AppWindow.Show(); 
        NavigateTo(typeof(TaskTile.Pages.SettingsPage));
        this.Activate();
    }

    // Removed old TrayMenu event handlers as we now use SystemTrayManager

    private void SetupWindow()
    {
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(TitleBarDragRegion);
        var appWindow = this.AppWindow;
        if (appWindow.TitleBar != null)
        {
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            int tbHeight = appWindow.TitleBar.Height > 0 ? appWindow.TitleBar.Height : 48;
            AppTitleBar.Height = tbHeight;
            AppTitleBarRow.Height = new GridLength(tbHeight);
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

            GroupsItem.Content = s.UseNichesTerminology ? "Niches" : "Groups";
        } catch { }
    }

    private string? _pendingNotificationPath;

    public void ShowInAppNotification(string title, string message, string? filePath = null)
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            _pendingNotificationPath = filePath;
            NotificationTitleText.Text = title;
            NotificationMessageText.Text = message;

            if (filePath == "FIX_POLICY")
            {
                NotificationActionBtn.Content = "Allow Pinning";
                NotificationActionBtn.Visibility = Visibility.Visible;
            }
            else if (filePath != null && (filePath == "CONFIRM_PIN" || filePath.StartsWith("CONFIRM_PIN:")))
            {
                NotificationActionBtn.Content = "Pin Now";
                NotificationActionBtn.Visibility = Visibility.Visible;
            }
            else if (!string.IsNullOrEmpty(filePath))
            {
                bool isDesktop = false;
                try
                {
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (filePath.StartsWith(desktopDir, StringComparison.OrdinalIgnoreCase))
                        isDesktop = true;
                }
                catch { }

                NotificationActionBtn.Content = isDesktop ? "Show on Desktop" : "Show in Folder";
                NotificationActionBtn.Visibility = Visibility.Visible;
            }
            else
            {
                NotificationActionBtn.Visibility = Visibility.Collapsed;
            }

            InAppNotificationBanner.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                InAppNotificationBanner.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        });
    }

    private void NotificationActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingNotificationPath == "FIX_POLICY")
        {
            TaskbarService.AllowPinningPolicy();
            ShowInAppNotification("Policy Updated", "Taskbar pinning policy has been enabled. You can now pin your groups!", null);
            return;
        }
        if (_pendingNotificationPath != null && _pendingNotificationPath.StartsWith("CONFIRM_PIN:"))
        {
            string rawId = _pendingNotificationPath.Substring("CONFIRM_PIN:".Length);
            InAppNotificationBanner.Visibility = Visibility.Collapsed;
            if (Guid.TryParse(rawId, out var gId))
            {
                var grp = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == gId);
                if (grp != null)
                {
                    TaskbarService.ExecutePin(grp);
                }
            }
            return;
        }

        if (!string.IsNullOrEmpty(_pendingNotificationPath))
        {
            try
            {
                if (System.IO.File.Exists(_pendingNotificationPath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_pendingNotificationPath}\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            catch { }
        }
        InAppNotificationBanner.Visibility = Visibility.Collapsed;
    }

    private void NotificationDismissBtn_Click(object sender, RoutedEventArgs e)
    {
        InAppNotificationBanner.Visibility = Visibility.Collapsed;
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

    private void TitleBarPaneToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
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
