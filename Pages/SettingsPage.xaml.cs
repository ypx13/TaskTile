using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskTile.Services;
using System;
using System.Linq;

namespace TaskTile.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitialized = false;

    public SettingsPage()
    {
        this.InitializeComponent();

        ThemeCombo.SelectedIndex    = SettingsService.Current.Theme;
        BackdropCombo.SelectedIndex  = SettingsService.Current.BackdropStyle;
        TitleAlignmentSlider.Value   = SettingsService.Current.TitleAlignment;
        TrayToggle.IsOn              = SettingsService.Current.EnableTrayIcon;
        BackgroundPopupToggle.IsOn   = SettingsService.Current.StartPopupsInBackground;
        StartupToggle.IsOn           = SettingsService.Current.RunAtStartup;
        WizardToggle.IsOn            = SettingsService.Current.DisableWizard;
        PinStartToggle.IsOn          = SettingsService.Current.PinToStart;
        SuppressExplorerToggle.IsOn  = SettingsService.Current.SuppressExplorer;
        PopupsConfigToggle.IsOn      = SettingsService.Current.ApplyGlobalConfigToPopups;
        LaunchSideCombo.SelectedIndex = Math.Max(0, (int)SettingsService.Current.LaunchSide - 1);
        GlobalMakeMainFocusToggle.IsOn = SettingsService.Current.GlobalMakeMainFocus;

        UpdateGlobalMakeMainFocusVisibility();

        bool isLight = SettingsService.Current.Theme == 1 || (SettingsService.Current.Theme == 0 && Application.Current.RequestedTheme == ApplicationTheme.Light);


        _isInitialized = true;
    }

    private void PopupsConfigToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.ApplyGlobalConfigToPopups = PopupsConfigToggle.IsOn;
        SettingsService.Save();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.Theme = ThemeCombo.SelectedIndex;
        
        bool isLight = SettingsService.Current.Theme == 1 || (SettingsService.Current.Theme == 0 && Application.Current.RequestedTheme == ApplicationTheme.Light);

        
        SettingsService.Save();
        App.MainWindowInstance?.ApplySettings();
    }

    private void BackdropCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.BackdropStyle = BackdropCombo.SelectedIndex;
        SettingsService.Save();
        App.MainWindowInstance?.ApplySettings();
    }

    private void TitleAlignmentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.TitleAlignment = (int)e.NewValue;
        SettingsService.Save();
    }

    private void LaunchSideCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.LaunchSide = (TaskTile.Models.LaunchSide)(LaunchSideCombo.SelectedIndex + 1);
        SettingsService.Save();
        UpdateGlobalMakeMainFocusVisibility();
    }

    private void UpdateGlobalMakeMainFocusVisibility()
    {
        if (GlobalMakeMainFocusContainer != null)
        {
            GlobalMakeMainFocusContainer.Visibility = (SettingsService.Current.LaunchSide == TaskTile.Models.LaunchSide.Center) 
                ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void GlobalDisableAnimationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return; var s = SettingsService.Current; s.DisableAnimation = GlobalDisableAnimationToggle.IsOn;
        SettingsService.Save();
    }
    
    private void GlobalDisableAutoHideToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return; var s = SettingsService.Current; s.DisableAutoHide = GlobalDisableAutoHideToggle.IsOn;
        SettingsService.Save();
    }

        private void GlobalDisableFloatToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        var s = SettingsService.Current;
        s.DisableFloat = GlobalDisableFloatToggle.IsOn;
        SettingsService.Save();
    }
    
    private void GlobalDisableRoundedCornersToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        var s = SettingsService.Current;
        s.DisableRoundedCorners = GlobalDisableRoundedCornersToggle.IsOn;
        SettingsService.Save();
    }

    private void GlobalMakeMainFocusToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.GlobalMakeMainFocus = GlobalMakeMainFocusToggle.IsOn;
        SettingsService.Save();
    }

    private void TrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.EnableTrayIcon = TrayToggle.IsOn;
        SettingsService.Save();
    }

    private void BackgroundPopupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.StartPopupsInBackground = BackgroundPopupToggle.IsOn;
        SettingsService.Save();
        App.MainWindowInstance?.InitializeBackgroundPopup();
    }

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.RunAtStartup = StartupToggle.IsOn;
        SettingsService.Save();
        
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (StartupToggle.IsOn)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue("TaskTile", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("TaskTile", false);
                }
            }
        }
        catch { }
    }

    private void WizardToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.DisableWizard = WizardToggle.IsOn;
        SettingsService.Save();
    }

    private void PinStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.PinToStart = PinStartToggle.IsOn;
        SettingsService.Save();
    }

    private void SuppressExplorerToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.SuppressExplorer = SuppressExplorerToggle.IsOn;
        SettingsService.Save();
    }

    // ─── Delete Data ─────────────────────────────────────────────────────────
    private int _deleteClicks = 0;
    private ContentDialog? _currentDeleteDialog;

    private async void DeleteData_Click(object sender, RoutedEventArgs e)
    {
        _deleteClicks = 0;
        var textBlock = new TextBlock
        {
            Text = "Deleting this app's data, will, and i say again, WILL FOREVER DELETE IT! ARE YOU SURE??",
            TextWrapping = TextWrapping.Wrap,
            RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform()
        };

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = -2, To = 2,
            Duration = new Duration(TimeSpan.FromMilliseconds(50)),
            AutoReverse = true,
            RepeatBehavior = Microsoft.UI.Xaml.Media.Animation.RepeatBehavior.Forever
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, textBlock.RenderTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "X");
        storyboard.Children.Add(animation);
        storyboard.Begin();

        var dialog = new ContentDialog
        {
            Title = "Warning",
            Content = textBlock,
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var style = new Style(typeof(Button));
        style.BasedOn = Application.Current.Resources["AccentButtonStyle"] as Style;
        style.Setters.Add(new Setter(Control.BackgroundProperty, new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkRed)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White)));
        dialog.PrimaryButtonStyle = style;

        dialog.PrimaryButtonClick += (s, args) =>
        {
            args.Cancel = true;
            _deleteClicks++;
            if (_deleteClicks >= 3)
            {
                dialog.Hide();
                var roaming = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile");
                var local   = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile");
                
                try { if (System.IO.Directory.Exists(roaming)) System.IO.Directory.Delete(roaming, true); } catch { }
                try { if (System.IO.Directory.Exists(local))   System.IO.Directory.Delete(local, true); } catch { }
                
                Application.Current.Exit();
            }
            else
            {
                textBlock.Text = $"Are you really sure? ({3 - _deleteClicks} clicks remaining)";
            }
        };

        _currentDeleteDialog = dialog;
        await dialog.ShowAsync();
    }

    // ─── Easter Eggs & Debug Access ──────────────────────────────────────────
    private int _madeByClicks = 0;
    private DateTime _lastMadeByClick = DateTime.MinValue;
    private Random _rand = new Random();

    private async void MadeBy_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if ((DateTime.Now - _lastMadeByClick).TotalSeconds > 3)
            _madeByClicks = 0;

        _lastMadeByClick = DateTime.Now;
        _madeByClicks++;

        if (_madeByClicks >= 10)
        {
            _madeByClicks = 0;

            // Step 1: DTM Warning dialog
            var warnTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            warnTitle.Children.Add(new FontIcon { Glyph = "\uE7BA", FontSize = 20, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 180, 0)) });
            warnTitle.Children.Add(new TextBlock { Text = "DTM mode..", FontWeight = Microsoft.UI.Text.FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

            var warnDialog = new ContentDialog
            {
                Title = warnTitle,
                Content = new TextBlock
                {
                    Text = "Are you sure you want to activate this? If you do activate this, you could be messing with potentially buggy settings.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                    MaxWidth = 300
                },
                PrimaryButtonText = "I understand the risks",
                CloseButtonText   = "Nevermind",
                DefaultButton     = ContentDialogButton.Close,
                XamlRoot          = XamlRoot
            };

            var warnResult = await warnDialog.ShowAsync();
            if (warnResult != ContentDialogResult.Primary) return;

            // Step 2: Passcode dialog
            var tb = new PasswordBox { PlaceholderText = "Enter developer code", Width = 240 };
            var passcodeDialog = new ContentDialog
            {
                Title = "DTM Access",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children = { new TextBlock { Text = "Enter the passcode to unlock DTM mode.", TextWrapping = TextWrapping.WrapWholeWords, MaxWidth = 280 }, tb }
                },
                PrimaryButtonText = "Unlock",
                CloseButtonText   = "Cancel",
                DefaultButton     = ContentDialogButton.Primary,
                XamlRoot          = XamlRoot
            };
            tb.KeyDown += (_, ke) => { if (ke.Key == Windows.System.VirtualKey.Enter) passcodeDialog.Hide(); };

            var result = await passcodeDialog.ShowAsync();
            if (result == ContentDialogResult.Primary && tb.Password == "26tasktile")
            {
                SettingsService.Current.PersistentDebugMode = true;
                SettingsService.Save();
                ShowDebugPanel();
            }
        }
    }



    private void TriggerStarWarsCredits()
    {
        StarWarsOverlay.Visibility = Visibility.Visible;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("TASKTILE v0.5");
        sb.AppendLine();
        foreach (var role in new[] { "DEVELOPER", "DESIGNER", "QA TEAM", "PROJECT MANAGER",
                                      "SPECIAL THANKS", "SOUND DIRECTOR", "CATERING", "CEO", "INTERN" })
        {
            sb.AppendLine(role);
            sb.AppendLine("ypx.13");
            sb.AppendLine();
        }
        CreditsText.Text = sb.ToString();

        StarWarsMusicPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri("https://freepd.com/music/Epic%20Boss%20Battle.mp3"));
        StarWarsMusicPlayer.MediaPlayer.Play();

        var transform = new Microsoft.UI.Xaml.Media.TranslateTransform();
        CreditsText.RenderTransform = transform;
        transform.Y = this.ActualHeight + 100;

        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = -1000,
            Duration = new Duration(TimeSpan.FromSeconds(15))
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, transform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Y");
        storyboard.Children.Add(anim);

        storyboard.Completed += (s, ev) =>
        {
            StarWarsOverlay.Visibility = Visibility.Collapsed;
            StarWarsMusicPlayer.MediaPlayer.Pause();
        };
        storyboard.Begin();
    }

    // ─── Debug Panel ──────────────────────────────────────────────────────────
    private bool _debugPanelExpanded = true;

    private void ShowDebugPanel()
    {
        _debugPanelExpanded = true;
        PopulateDebugPanel();
        DebugOuterContainer.Visibility = Visibility.Visible;
        DebugOverlay.Visibility = Visibility.Visible;
        PullTabArrow.Glyph = "\uE76C"; // chevron right (collapse)

        DebugOuterContainer.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { X = 380 };
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, DebugOuterContainer.RenderTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "X");
        sb.Children.Add(anim);
        sb.Begin();
    }

    private void HideDebugPanel()
    {
        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = 380,
            Duration = new Duration(TimeSpan.FromMilliseconds(240)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, DebugOuterContainer.RenderTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "X");
        sb.Children.Add(anim);
        sb.Completed += (_, _) => DebugOuterContainer.Visibility = Visibility.Collapsed;
        sb.Begin();
    }

    private void DebugPullTab_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (_debugPanelExpanded)
        {
            // Collapse panel but keep pull tab visible
            _debugPanelExpanded = false;
            PullTabArrow.Glyph = "\uE76B"; // chevron left (expand)
            DebugOverlay.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Expand panel
            _debugPanelExpanded = true;
            PullTabArrow.Glyph = "\uE76C"; // chevron right (collapse)
            DebugOverlay.Visibility = Visibility.Visible;
        }
    }

    private void PopulateDebugPanel()
    {
        var s = SettingsService.Current;
        // System
        DbgVersion.Text   = $"ver  v0.5  |  PDebug={s.PersistentDebugMode}";
        DbgOS.Text        = $"os   {Environment.OSVersion.Version}";
        DbgMachine.Text   = $"host {Environment.MachineName}";
        DbgUser.Text      = $"user {Environment.UserName}";
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        DbgUptime.Text    = $"up   {(int)uptime.TotalHours}h {uptime.Minutes}m";
        // App config
        DbgBackdrop.Text  = $"bk   {s.BackdropStyle switch { 0 => "Acrylic", 1 => "Mica", 2 => "Mica Alt", 3 => "None (OLED)", _ => s.BackdropStyle.ToString() }}";
        DbgTheme.Text     = $"thm  {s.Theme switch { 1 => "Light", 2 => "Dark", _ => "System" }}";
        DbgLaunchSide.Text= $"side {s.LaunchSide}";
        // Groups
        try
        {
            var gf = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile", "groups.json");
            DbgGroups.Text = System.IO.File.Exists(gf)
                ? $"grps {System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(gf)).RootElement.GetArrayLength()}"
                : "grps 0";
        }
        catch { DbgGroups.Text = "grps (err)"; }
        // Bar detection
        DbgDetected.Text = $"bar  {(string.IsNullOrEmpty(s.LastDetectedBar) ? "none" : s.LastDetectedBar)}";
        var detResult = BarDetectionService.Detect();
        DbgDetectionResult.Text = detResult.Detected
            ? $"det  {detResult.BarName}: {detResult.Detail[..Math.Min(60, detResult.Detail.Length)]}..."
            : "det  clean";
        // Paths
        DbgSettingsPath.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTile", "settings.json");
    }

    private void DbgRunDetection_Click(object sender, RoutedEventArgs e)
    {
        DbgDetectionResult.Text = "Scanning...";
        var result = BarDetectionService.Detect();
        DbgDetectionResult.Text = result.Detected
            ? $"✅ Detected: {result.BarName}\n{result.Detail}"
            : "✔ No known bar modifications detected.";
    }


    private async void DbgTriggerDemo_Click(object sender, RoutedEventArgs e)
    {
        // Trigger the exact same dialog flow as the real first-time bar detection alert
        var mw = App.MainWindowInstance;
        if (mw?.Content?.XamlRoot == null) return;

        var warnTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        warnTitlePanel.Children.Add(new FontIcon { Glyph = "\uE7BA", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 180, 0)) });
        warnTitlePanel.Children.Add(new TextBlock { Text = "Adjustment Needed", FontWeight = Microsoft.UI.Text.FontWeights.Bold });

        var warnDialog = new ContentDialog
        {
            Title = warnTitlePanel,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"A Windows modification has been detected:\n\n\u2022 YASB (Demo)\n\nYASB (Yet Another Status Bar) config directory or process detected.\n\nIf this is a false positive, please report it in the Developer Sanctuary Discord server ",
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                    },
                    new HyperlinkButton
                    {
                        Content = "TaskTile - Fluent app groups",
                        NavigateUri = new Uri("https://discord.com/channels/714581497222398064/1485996063608406086")
                    },
                    new TextBlock { Text = "with the log located at:" },
                    new HyperlinkButton
                    {
                        Content = TaskTile.Services.BarDetectionService.LogPath,
                        NavigateUri = new Uri("file://" + TaskTile.Services.BarDetectionService.LogPath.Replace('\\', '/'))
                    }
                }
            },
            PrimaryButtonText   = "Yes, I would like to adjust",
            CloseButtonText     = "No",
            DefaultButton       = ContentDialogButton.Close,
            XamlRoot            = XamlRoot
        };

        var warnResult = await warnDialog.ShowAsync();
        if (warnResult == ContentDialogResult.Primary)
        {
            // Side picker - same as real flow
            var sideDialog = new ContentDialog
            {
                Title           = "Which side should app groups launch from?",
                CloseButtonText = "Cancel",
                XamlRoot        = XamlRoot
            };

            var sideGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var sides = new (string label, string glyph, Models.LaunchSide side, int row, int col, bool isExp)[]
            {
                ("Top",    "\uE70E", Models.LaunchSide.Top,    0, 0, false),
                ("Bottom", "\uE70D", Models.LaunchSide.Bottom, 0, 1, false),
                ("Left",   "\uE76B", Models.LaunchSide.Left,   1, 0, true),
                ("Right",  "\uE76C", Models.LaunchSide.Right,  1, 1, true),
            };

            foreach (var sd in sides)
            {
                var btn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment   = VerticalAlignment.Stretch,
                    MinHeight = 100,
                    CornerRadius = new CornerRadius(8),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255))
                };
                var panel = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                panel.Children.Add(new FontIcon { Glyph = sd.glyph, FontSize = 32 });
                panel.Children.Add(new TextBlock { Text = sd.label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center });
                if (sd.isExp)
                {
                    var tag = new Border
                    {
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 255, 185, 0)),
                        BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(128, 255, 185, 0)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    tag.Child = new TextBlock
                    {
                        Text = "Experimental",
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                        FontSize = 10,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    };
                    panel.Children.Add(tag);
                }
                btn.Content = panel;

                var capSide = sd.side;
                btn.Click += (_, _) =>
                {
                    sideDialog.Hide();
                    SettingsService.Current.LaunchSide = capSide;
                    SettingsService.Save();
                    mw.ApplySettings();
                    // Sync the settings page combo
                    LaunchSideCombo.SelectedIndex = (int)capSide;
                };
                Grid.SetRow(btn, sd.row);
                Grid.SetColumn(btn, sd.col);
                sideGrid.Children.Add(btn);
            }
            sideDialog.Content = sideGrid;
            await sideDialog.ShowAsync();
        }
    }

    private void DbgCopy_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join("\n", new[]
        {
            DbgVersion.Text, DbgBackdrop.Text, DbgTheme.Text,
            DbgLaunchSide.Text, DbgDetected.Text, DbgGroups.Text,
            DbgSettingsPath.Text, DbgDetectionResult.Text
        });
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }

    private void DbgDisable_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.PersistentDebugMode = false;
        SettingsService.Save();
        HideDebugPanel();
    }

    private void DbgFrostedGlass_Click(object sender, RoutedEventArgs e)
    {
        SettingsService.Current.BackdropStyle = 3; // index 3 = Frosted Glass
        SettingsService.Save();
        App.MainWindowInstance?.ApplySettings();
        PopulateDebugPanel();
    }
}
