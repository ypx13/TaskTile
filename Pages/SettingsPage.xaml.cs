using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI;
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

        MixUIToggle.IsOn             = SettingsService.Current.YpxMixUI;
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
        RunningIndicatorStyleCombo.SelectedIndex = Math.Clamp(SettingsService.Current.RunningIndicatorStyle, 0, 1);
        GlobalMakeMainFocusToggle.IsOn = SettingsService.Current.GlobalMakeMainFocus;

        GroupCardStyleCombo.SelectedIndex = Math.Clamp(SettingsService.Current.GroupCardStyle, 0, 2);
        PinToTaskbarIntentToggle.IsOn = SettingsService.Current.UsePinToTaskbarIntent;
        UseNichesToggle.IsOn = SettingsService.Current.UseNichesTerminology;
        InstantPinToggle.IsOn = SettingsService.Current.InstantTaskbarPin;
        PickerIconsToggle.IsOn = SettingsService.Current.ShowPickerIcons;
        PickerPathsToggle.IsOn = SettingsService.Current.ShowPickerFilePaths;

        GlobalDisableAnimationToggle.IsOn = SettingsService.Current.DisableAnimation;
        AppEntranceAnimationToggle.IsOn = SettingsService.Current.EnableAppEntranceAnimation;
        AdaptiveRefreshRateToggle.IsOn = SettingsService.Current.AdaptiveRefreshRateAnimation;
        double speed = SettingsService.Current.AnimationSpeedMultiplier >= 0.2 ? SettingsService.Current.AnimationSpeedMultiplier : 1.0;
        AnimationSpeedSlider.Value = speed;
        AnimationSpeedValueText.Text = $"{speed:0.0}x";
        GlobalDisableAutoHideToggle.IsOn  = SettingsService.Current.DisableAutoHide;
        GlobalDisableFloatToggle.IsOn     = SettingsService.Current.DisableFloat;
        GlobalDisableRoundedCornersToggle.IsOn = SettingsService.Current.DisableRoundedCorners;
        GlobalTaskbarGapSlider.Value      = SettingsService.Current.TaskbarGap;
        if (ShowAdminShieldToggle != null) ShowAdminShieldToggle.IsOn = SettingsService.Current.ShowAdminShieldBadge;
        if (AdminShieldColorCombo != null) AdminShieldColorCombo.SelectedIndex = Math.Clamp(SettingsService.Current.AdminShieldColor, 0, 1);
        if (ShowNotificationBadgesToggle != null) ShowNotificationBadgesToggle.IsOn = SettingsService.Current.ShowNotificationBadges;
        if (EnableBackupSystemToggle != null) EnableBackupSystemToggle.IsOn = SettingsService.Current.EnableBackupSystem;
        if (OpenBackupButton != null) OpenBackupButton.IsEnabled = SettingsService.Current.EnableBackupSystem;
        if (GrayscaleTaskbarIconToggle != null) GrayscaleTaskbarIconToggle.IsOn = SettingsService.Current.UseGrayscaleTaskbarIcon;

        UpdateGlobalMakeMainFocusVisibility();
        UpdateTaskbarPinningState();

        bool isLight = SettingsService.Current.Theme == 1 || (SettingsService.Current.Theme == 0 && Application.Current.RequestedTheme == ApplicationTheme.Light);

        _isInitialized = true;
        SetupTitleAlignmentPreview();
        this.Loaded += SettingsPage_Loaded;
    }

    private readonly System.Text.StringBuilder _secretKeyBuffer = new();
    private DateTime _lastSecretKeyTime = DateTime.MinValue;

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Current.PersistentDebugMode)
        {
            ShowDebugPanel();
        }

        this.KeyDown -= SettingsPage_KeyDown;
        this.KeyDown += SettingsPage_KeyDown;
    }

    private void SettingsPage_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if ((DateTime.Now - _lastSecretKeyTime).TotalSeconds > 3)
            _secretKeyBuffer.Clear();

        _lastSecretKeyTime = DateTime.Now;

        char c = (char)0;
        var key = e.Key;
        if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
            c = (char)('a' + (key - Windows.System.VirtualKey.A));
        else if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
            c = (char)('0' + (key - Windows.System.VirtualKey.Number0));

        if (c != (char)0)
        {
            _secretKeyBuffer.Append(c);
            if (_secretKeyBuffer.Length > 20)
                _secretKeyBuffer.Remove(0, _secretKeyBuffer.Length - 20);

            string buf = _secretKeyBuffer.ToString();
            if (buf.EndsWith("tasktile26") || buf.EndsWith("26tasktile"))
            {
                _secretKeyBuffer.Clear();
                SettingsService.Current.PersistentDebugMode = true;
                SettingsService.Save();
                ShowDebugPanel();
            }
        }
    }

    private void UpdateTaskbarPinningState()
    {
        bool isTaskbar = PinToTaskbarIntentToggle?.IsOn == true;
        if (GlobalConfigInfoBar != null)
        {
            GlobalConfigInfoBar.Message = isTaskbar
                ? "These settings apply across TaskTile, its launchers, and taskbar integration."
                : "These settings apply across TaskTile, its launchers, and desktop integration.";
        }
        if (InstantPinCard != null)
        {
            InstantPinCard.Visibility = isTaskbar ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void GroupCardStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.GroupCardStyle = GroupCardStyleCombo.SelectedIndex;
        SettingsService.Save();
    }

    private async void PinToTaskbarIntentToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        if (PinToTaskbarIntentToggle.IsOn)
        {
            bool ok = await ShowExperimentalWarningAsync();
            if (!ok)
            {
                PinToTaskbarIntentToggle.IsOn = false;
                UpdateTaskbarPinningState();
                return;
            }
        }
        SettingsService.Current.UsePinToTaskbarIntent = PinToTaskbarIntentToggle.IsOn;
        SettingsService.Save();
        if (PinToTaskbarIntentToggle.IsOn)
        {
            TaskbarService.WarmupBridge();
        }
        UpdateTaskbarPinningState();
    }

    private async void UseNichesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        if (UseNichesToggle.IsOn)
        {
            bool ok = await ShowExperimentalWarningAsync();
            if (!ok)
            {
                UseNichesToggle.IsOn = false;
                return;
            }
        }
        SettingsService.Current.UseNichesTerminology = UseNichesToggle.IsOn;
        SettingsService.Save();
    }

    private void InstantPinToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.InstantTaskbarPin = InstantPinToggle.IsOn;
        SettingsService.Save();
    }

    private void PickerIconsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.ShowPickerIcons = PickerIconsToggle.IsOn;
        SettingsService.Save();
    }

    private void PickerPathsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.ShowPickerFilePaths = PickerPathsToggle.IsOn;
        SettingsService.Save();
    }

    private async System.Threading.Tasks.Task<bool> ShowExperimentalWarningAsync()
    {
        try
        {
            var contentGrid = new Grid
            {
                Width = 400,
                Margin = new Thickness(0, 4, 0, 0)
            };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top row: Warning symbol and 3D character closer together and centered
            var topPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 24,
                Margin = new Thickness(0, 4, 0, 12)
            };

            // 3D Floating Warning Symbol with ground drop shadow
            var warningHost = new Grid
            {
                Width = 110,
                Height = 185,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var shadowScale = new ScaleTransform { CenterX = 35, CenterY = 7 };
            var warningGroundShadow = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 66,
                Height = 14,
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
                Opacity = 0.36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 10),
                RenderTransform = shadowScale
            };
            warningHost.Children.Add(warningGroundShadow);

            var floatTransform = new TranslateTransform();
            var iconContainer = new Grid
            {
                Width = 86,
                Height = 86,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16),
                RenderTransform = floatTransform
            };

            // 3D Extruded Depth Layer 1: Ambient Drop Shadow behind glyph
            var shadowGlyph = new FontIcon
            {
                Glyph = "\uE7BA",
                FontSize = 60,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(110, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 7, 0, 0)
            };
            // 3D Extruded Depth Layer 2: Bevel extrusion edge
            var extrudeGlyph = new FontIcon
            {
                Glyph = "\uE7BA",
                FontSize = 60,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 195, 120, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 2.5, 0, 0)
            };
            // 3D Front Face: Vibrant gold/yellow glyph
            var frontGlyph = new FontIcon
            {
                Glyph = "\uE7BA",
                FontSize = 60,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 195, 0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            iconContainer.Children.Add(shadowGlyph);
            iconContainer.Children.Add(extrudeGlyph);
            iconContainer.Children.Add(frontGlyph);
            warningHost.Children.Add(iconContainer);
            topPanel.Children.Add(warningHost);

            // Floating 3D Storyboard — subtle, natural, calm Fluent breathing
            var floatSb = new Storyboard();
            var floatAnim = new DoubleAnimation
            {
                From = 0,
                To = -5, // Calm natural float (5px instead of 12px)
                Duration = new Duration(TimeSpan.FromMilliseconds(2200)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(floatAnim, floatTransform);
            Storyboard.SetTargetProperty(floatAnim, "Y");
            floatSb.Children.Add(floatAnim);

            // Shadow subtly darkens when symbol is close (0.36), lightens slightly when further away (0.28)
            var shadowOpacityAnim = new DoubleAnimation
            {
                From = 0.36,
                To = 0.28,
                Duration = new Duration(TimeSpan.FromMilliseconds(2200)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(shadowOpacityAnim, warningGroundShadow);
            Storyboard.SetTargetProperty(shadowOpacityAnim, "Opacity");
            floatSb.Children.Add(shadowOpacityAnim);

            // Scale dynamically in sync (1.0 -> 0.95)
            var shadowScaleXAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.95,
                Duration = new Duration(TimeSpan.FromMilliseconds(2200)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(shadowScaleXAnim, shadowScale);
            Storyboard.SetTargetProperty(shadowScaleXAnim, "ScaleX");
            floatSb.Children.Add(shadowScaleXAnim);

            var shadowScaleYAnim = new DoubleAnimation
            {
                From = 1.0,
                To = 0.95,
                Duration = new Duration(TimeSpan.FromMilliseconds(2200)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(shadowScaleYAnim, shadowScale);
            Storyboard.SetTargetProperty(shadowScaleYAnim, "ScaleY");
            floatSb.Children.Add(shadowScaleYAnim);

            floatSb.Begin();

            string charPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "yp_experimental.png");
            string shadowPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "yp_experimental_shadow.png");
            if (System.IO.File.Exists(charPath))
            {
                var charHost = new Grid
                {
                    Width = 150,
                    Height = 185,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Ground drop shadow beneath feet
                var groundShadow = new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 86,
                    Height = 13,
                    Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0)),
                    Opacity = 0.35,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(19, 0, 0, 2)
                };
                charHost.Children.Add(groundShadow);

                if (System.IO.File.Exists(shadowPath))
                {
                    var shadowImage = new Image
                    {
                        Source = new BitmapImage(new Uri(shadowPath)),
                        Width = 140,
                        Height = 175,
                        Stretch = Stretch.Uniform,
                        Opacity = 0.18,
                        Margin = new Thickness(3, 0, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    charHost.Children.Add(shadowImage);
                }

                var charImage = new Image
                {
                    Source = new BitmapImage(new Uri(charPath)),
                    Width = 140,
                    Height = 175,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                charHost.Children.Add(charImage);

                topPanel.Children.Add(charHost);
            }
            Grid.SetRow(topPanel, 0);
            contentGrid.Children.Add(topPanel);

            // Bottom row: Descriptive text with ThemeResource font styling
            var descText = new TextBlock
            {
                Text = "We're not quite sure how stable this is yet. Turning this feature on may lead to unwanted hiccups or unexpected behavior. Are you sure you want to proceed?",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                LineHeight = 19,
                Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var sc) && sc is Brush scb ? scb : new SolidColorBrush(Windows.UI.Color.FromArgb(200, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(8, 12, 8, 4)
            };
            Grid.SetRow(descText, 1);
            contentGrid.Children.Add(descText);

            var dialog = new ContentDialog
            {
                Title = "Experimental Feature",
                Content = contentGrid,
                PrimaryButtonText = "Okay",
                CloseButtonText = "Nevermind",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot ?? App.MainWindowInstance?.Content?.XamlRoot,
                RequestedTheme = this.ActualTheme,
                CornerRadius = new CornerRadius(12)
            };

            var res = await dialog.ShowAsync();
            return res == ContentDialogResult.Primary;
        }
        catch
        {
            return true;
        }
    }

    private void MixUIToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.YpxMixUI = MixUIToggle.IsOn;
        SettingsService.Save();

        App.ApplyCubeKitGlowUI(MixUIToggle.IsOn);

        if (MixUIToggle.IsOn)
        {
            PlayMixUIFizzEffect();
        }
    }

    private void PlayMixUIFizzEffect()
    {
        if (EffectCanvas == null || MixUIToggle == null) return;

        try
        {
            var transform = MixUIToggle.TransformToVisual(EffectCanvas);
            var pt = transform.TransformPoint(new Windows.Foundation.Point(MixUIToggle.ActualWidth / 2, MixUIToggle.ActualHeight / 2));

            var rand = new Random();
            int bubbleCount = 14;

            string ocPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "ypx_oc.png");
            bool hasOc = System.IO.File.Exists(ocPath);

            for (int i = 0; i < bubbleCount; i++)
            {
                double size = rand.Next(18, 30);
                var bubble = new Grid
                {
                    Width = size,
                    Height = size,
                    Opacity = 1.0,
                    RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5)
                };

                // Tinted blue bubble border
                var circle = new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(size / 2),
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(55, 30, 144, 255)),
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(190, 135, 206, 250)),
                    BorderThickness = new Thickness(1.5)
                };
                bubble.Children.Add(circle);

                // OC Head inside bubble
                if (hasOc)
                {
                    var img = new Image
                    {
                        Source = new BitmapImage(new Uri(ocPath)),
                        Width = size * 0.85,
                        Height = size * 0.85,
                        Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    bubble.Children.Add(img);
                }

                var tt = new TranslateTransform();
                var st = new ScaleTransform { ScaleX = 0.25, ScaleY = 0.25 };
                var tg = new TransformGroup();
                tg.Children.Add(st);
                tg.Children.Add(tt);
                bubble.RenderTransform = tg;

                // Initial position near toggle
                double startX = pt.X + rand.Next(-20, 20) - (size / 2);
                double startY = pt.Y + rand.Next(-10, 10) - (size / 2);
                Canvas.SetLeft(bubble, startX);
                Canvas.SetTop(bubble, startY);

                EffectCanvas.Children.Add(bubble);

                // Animate: shoot out and float up like soda fizz, then pop / fade
                double targetX = rand.Next(-50, 50);
                double targetY = -rand.Next(40, 100);
                int durationMs = rand.Next(380, 700);

                var sb = new Storyboard();

                // Fade out
                var daOpacity = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(daOpacity, bubble);
                Storyboard.SetTargetProperty(daOpacity, "Opacity");
                sb.Children.Add(daOpacity);

                // Float up and drift X
                var daX = new DoubleAnimation
                {
                    To = targetX,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(daX, tt);
                Storyboard.SetTargetProperty(daX, "X");
                sb.Children.Add(daX);

                var daY = new DoubleAnimation
                {
                    To = targetY,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(daY, tt);
                Storyboard.SetTargetProperty(daY, "Y");
                sb.Children.Add(daY);

                // Pop / scale up quickly then fizz away
                var daScaleX = new DoubleAnimationUsingKeyFrames();
                daScaleX.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.35)), Value = 1.15 });
                daScaleX.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), Value = 0.1 });
                Storyboard.SetTarget(daScaleX, st);
                Storyboard.SetTargetProperty(daScaleX, "ScaleX");
                sb.Children.Add(daScaleX);

                var daScaleY = new DoubleAnimationUsingKeyFrames();
                daScaleY.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.35)), Value = 1.15 });
                daScaleY.KeyFrames.Add(new SplineDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), Value = 0.1 });
                Storyboard.SetTarget(daScaleY, st);
                Storyboard.SetTargetProperty(daScaleY, "ScaleY");
                sb.Children.Add(daScaleY);

                sb.Completed += (_, _) =>
                {
                    EffectCanvas.Children.Remove(bubble);
                };
                sb.Begin();
            }
        }
        catch { }
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

    // ─── Title Alignment Preview ─────────────────────────────────────────────

    private DispatcherTimer? _previewHideTimer;
    private bool _previewVisible = false;
    private Storyboard? _previewOpacitySb;
    // Container height: compact 48px expansion so preview is fully visible while sections barely move
    private const double PreviewContainerFullHeight = 48;

    private void SetupTitleAlignmentPreview()
    {
        if (PreviewTitleHost != null)
            PreviewTitleHost.SizeChanged += (s, e) => UpdatePreviewTitlePosition(SettingsService.Current.TitleAlignment, animate: false);

        UpdatePreviewTitlePosition(SettingsService.Current.TitleAlignment, animate: false);

        // Immediate responsive trigger on pointer press (even if thumb hasn't moved yet)
        if (TitleAlignmentSlider != null)
        {
            TitleAlignmentSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((s, e) => ShowPreview()), true);
        }

        _previewHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        _previewHideTimer.Tick += (s, e) =>
        {
            _previewHideTimer!.Stop();
            FadeOutPreview();
        };
    }

    private void ShowPreview()
    {
        // Suppressed per user request
    }

    private void FadeOutPreview()
    {
        if (TitlePreviewHost == null || TitlePreviewContainer == null || !_previewVisible) return;

        _previewOpacitySb?.Stop();

        // 1. Fade out opacity first
        var opacityAnim = new DoubleAnimation
        {
            From = TitlePreviewHost.Opacity,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        _previewOpacitySb = new Storyboard();
        Storyboard.SetTarget(opacityAnim, TitlePreviewHost);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        _previewOpacitySb.Children.Add(opacityAnim);
        _previewOpacitySb.Completed += (s, e) =>
        {
            // 2. Once faded out, collapse the height smoothly
            AnimateContainerHeight(TitlePreviewContainer.Height, 0, 200, EasingMode.EaseInOut, () =>
            {
                _previewVisible = false;
            });
        };
        _previewOpacitySb.Begin();
    }

    private DispatcherTimer? _heightAnimTimer;
    private void AnimateContainerHeight(double from, double to, int durationMs, EasingMode easing, Action? onComplete)
    {
        _heightAnimTimer?.Stop();
        if (Math.Abs(from - to) < 0.5)
        {
            TitlePreviewContainer.Height = to;
            onComplete?.Invoke();
            return;
        }

        TitlePreviewContainer.Height = from;
        double elapsed = 0;
        double interval = 1000.0 / 60.0; // ~60fps
        var ease = new CubicEase { EasingMode = easing };

        _heightAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _heightAnimTimer.Tick += (s, e) =>
        {
            elapsed += interval;
            double progress = Math.Min(1.0, elapsed / durationMs);
            double easedProgress = ease.Ease(progress);
            TitlePreviewContainer.Height = from + (to - from) * easedProgress;

            if (progress >= 1.0)
            {
                _heightAnimTimer!.Stop();
                TitlePreviewContainer.Height = to;
                onComplete?.Invoke();
            }
        };
        _heightAnimTimer.Start();
    }

    private void UpdatePreviewTitlePosition(int align, bool animate = true)
    {
        if (PreviewTitleHost == null || PreviewTitleText == null || PreviewTitleTransform == null) return;

        double hostWidth = PreviewTitleHost.ActualWidth > 0 ? PreviewTitleHost.ActualWidth : 172;
        double textWidth = PreviewTitleText.ActualWidth > 0 ? PreviewTitleText.ActualWidth : 48;

        double targetX = align switch
        {
            0 => 0,
            1 => Math.Max(0, (hostWidth - textWidth) / 2.0),
            2 => Math.Max(0, hostWidth - textWidth),
            _ => 0
        };

        if (animate && Math.Abs(PreviewTitleTransform.X - targetX) > 0.5)
        {
            var anim = new DoubleAnimation
            {
                To = targetX,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, PreviewTitleTransform);
            Storyboard.SetTargetProperty(anim, "X");
            sb.Children.Add(anim);
            sb.Begin();
        }
        else
        {
            PreviewTitleTransform.X = targetX;
        }
    }

    private void TitleAlignmentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        int align = (int)Math.Round(e.NewValue);
        SettingsService.Current.TitleAlignment = align;
        SettingsService.Save();

        ShowPreview();
        _previewHideTimer?.Stop();
        _previewHideTimer?.Start();

        UpdatePreviewTitlePosition(align);
    }

    private void RunningIndicatorStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.RunningIndicatorStyle = RunningIndicatorStyleCombo.SelectedIndex;
        SettingsService.Save();
    }

    private void ShowAdminShieldToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.ShowAdminShieldBadge = ShowAdminShieldToggle.IsOn;
        SettingsService.Save();
    }

    private void ShowNotificationBadgesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.ShowNotificationBadges = ShowNotificationBadgesToggle.IsOn;
        SettingsService.Save();
        _ = TaskTile.Services.NotificationBadgeService.RefreshCountsAsync();
    }

    private void AdminShieldColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.AdminShieldColor = AdminShieldColorCombo.SelectedIndex >= 0 ? AdminShieldColorCombo.SelectedIndex : 0;
        SettingsService.Save();
    }

    private async void EnableBackupSystemToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        if (EnableBackupSystemToggle.IsOn)
        {
            bool ok = await ShowExperimentalWarningAsync();
            if (!ok)
            {
                EnableBackupSystemToggle.IsOn = false;
                if (OpenBackupButton != null)
                {
                    OpenBackupButton.IsEnabled = false;
                }
                return;
            }
        }
        SettingsService.Current.EnableBackupSystem = EnableBackupSystemToggle.IsOn;
        SettingsService.Save();
        if (OpenBackupButton != null)
        {
            OpenBackupButton.IsEnabled = EnableBackupSystemToggle.IsOn;
        }
    }

    private void OpenBackup_Click(object sender, RoutedEventArgs e)
    {
        App.MainWindowInstance?.NavigateTo(typeof(TaskTile.Pages.BackupPage));
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

    private void AppEntranceAnimationToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        var s = SettingsService.Current;
        s.EnableAppEntranceAnimation = AppEntranceAnimationToggle.IsOn;
        SettingsService.Save();
    }

    private void AdaptiveRefreshRateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        var s = SettingsService.Current;
        s.AdaptiveRefreshRateAnimation = AdaptiveRefreshRateToggle.IsOn;
        SettingsService.Save();
    }

    private void AnimationSpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (AnimationSpeedValueText != null)
        {
            AnimationSpeedValueText.Text = $"{e.NewValue:0.0}x";
        }
        if (!_isInitialized) return;
        var s = SettingsService.Current;
        s.AnimationSpeedMultiplier = Math.Round(e.NewValue, 2);
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
    
        private void GlobalTaskbarGapSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isInitialized) return;
        int val = (int)Math.Round(e.NewValue);
        SettingsService.Current.TaskbarGap = val;
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

    private void GrayscaleTaskbarIconToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        SettingsService.Current.UseGrayscaleTaskbarIcon = GrayscaleTaskbarIconToggle.IsOn;
        SettingsService.Save();
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
            var entered = tb.Password?.Trim() ?? "";
            if (result == ContentDialogResult.Primary && 
                (entered.Equals("26tasktile", StringComparison.OrdinalIgnoreCase) || 
                 entered.Equals("tasktile26", StringComparison.OrdinalIgnoreCase) ||
                 entered.Equals("tasktile", StringComparison.OrdinalIgnoreCase) ||
                 entered.Equals("dtm", StringComparison.OrdinalIgnoreCase)))
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
        sb.AppendLine("TASKTILE v0.7.5");
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

    public void ShowDebugPanel()
    {
        _debugPanelExpanded = true;
        PopulateDebugPanel();
        DebugOuterContainer.Visibility = Visibility.Visible;
        DebugOverlay.Visibility = Visibility.Visible;
        PullTabArrow.Glyph = "\uE76C"; // chevron right (collapse)

        DebugOuterContainer.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform { X = 420 };
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
            To = 420,
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
            PopulateDebugPanel();
        }
    }

    private void PopulateDebugPanel()
    {
        var s = SettingsService.Current;
        var (major, minor, patch, fullVer) = DebugDiagnosticService.GetVersionInfo();

        DbgVersionTitle.Text = fullVer;
        DbgSemVerPill.Text = $"M:{major} m:{minor} p:{patch}";

        DbgGitBranchCommit.Text = $"branch {DebugDiagnosticService.GetGitBranchAndCommit()}";
        DbgGitChanges.Text = $"chgs   {DebugDiagnosticService.GetChangesSince07()}";
        DbgGitPush.Text = $"github {DebugDiagnosticService.GetGitHubPushStatus()}";
        DbgGitWorkingTree.Text = $"tree   {DebugDiagnosticService.GetWorkingTreeStatus()}";

        // Hotkey
        bool hookActive = HotkeyService.Instance.IsHookActive;
        var hotkeys = DebugDiagnosticService.GetHotkeyStatus();
        DbgHotkeyHook.Text = $"hook   {(hookActive ? "Active (Hooked to HWND)" : "Inactive")}";
        DbgHotkeyDetails.Text = hotkeys.Count > 0
            ? string.Join(", ", hotkeys.Select(h => $"[{h.Shortcut}] -> {h.GroupName}"))
            : "No radial hotkeys registered in TaskTile.";

        // Logs summary
        try
        {
            string detLog = BarDetectionService.LogPath;
            if (System.IO.File.Exists(detLog))
            {
                var fi = new System.IO.FileInfo(detLog);
                DbgLogSummaryText.Text = $"detection.log ({fi.Length} bytes, updated {fi.LastWriteTime:HH:mm:ss})";
            }
            else
            {
                DbgLogSummaryText.Text = "No active logs present.";
            }
        }
        catch { DbgLogSummaryText.Text = "Log directory ready"; }

        // Fake running toggle
        DbgFakeRunningToggle.IsOn = AppRunningService.FakeAppsRunningEnabled;

        // Populate groups for jumplist test
        DbgJumpListGroupCombo.Items.Clear();
        foreach (var g in GroupService.Instance.Groups)
        {
            DbgJumpListGroupCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{g.Name} ({g.Apps.Count} apps)",
                Tag = g.Id
            });
        }
        if (DbgJumpListGroupCombo.Items.Count > 0)
            DbgJumpListGroupCombo.SelectedIndex = 0;

        // Bar detection
        DbgDetected.Text = $"bar  {(string.IsNullOrEmpty(s.LastDetectedBar) ? "none" : s.LastDetectedBar)}";
        var detResult = BarDetectionService.Detect();
        DbgDetectionResult.Text = detResult.Detected
            ? $"det  {detResult.BarName}: {detResult.Detail[..Math.Min(60, detResult.Detail.Length)]}..."
            : "det  clean";

        // System
        DbgOS.Text      = $"os   {Environment.OSVersion.Version}";
        DbgMachine.Text = $"host {Environment.MachineName}";
        DbgUser.Text    = $"user {Environment.UserName}";
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        DbgUptime.Text  = $"up   {(int)uptime.TotalHours}h {uptime.Minutes}m";
        DbgAppStats.Text= $"mem  {System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB | Grps: {GroupService.Instance.Groups.Count}";
    }

    private void DbgOpenRoamingFolder_Click(object sender, RoutedEventArgs e)
    {
        DebugDiagnosticService.OpenUserDataFolder(local: false);
    }

    private void DbgOpenLocalFolder_Click(object sender, RoutedEventArgs e)
    {
        DebugDiagnosticService.OpenUserDataFolder(local: true);
    }

    private void DbgExportLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = DebugDiagnosticService.ExportLogs();
            DbgLogActionStatusText.Text = $"✔ Exported to Desktop: {System.IO.Path.GetFileName(path)}";
            DbgLogActionStatusText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            DbgLogActionStatusText.Text = $"Export error: {ex.Message}";
            DbgLogActionStatusText.Visibility = Visibility.Visible;
        }
    }

    private void DbgDeleteLogs_Click(object sender, RoutedEventArgs e)
    {
        var (ok, msg) = DebugDiagnosticService.DeleteLogs();
        DbgLogActionStatusText.Text = ok ? $"✔ {msg}" : $"❌ {msg}";
        DbgLogActionStatusText.Visibility = Visibility.Visible;
        PopulateDebugPanel();
    }

    private void DbgFakeRunningToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized) return;
        AppRunningService.FakeAppsRunningEnabled = DbgFakeRunningToggle.IsOn;
    }

    private void DbgAddTestJumpList_Click(object sender, RoutedEventArgs e)
    {
        Guid targetGroupId = Guid.Empty;
        string targetGroupName = "App Group";

        if (DbgJumpListGroupCombo.SelectedItem is ComboBoxItem item && item.Tag is Guid gid)
        {
            targetGroupId = gid;
            var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == gid);
            if (group != null) targetGroupName = group.Name;
        }
        else if (GroupService.Instance.Groups.Count > 0)
        {
            var first = GroupService.Instance.Groups[0];
            targetGroupId = first.Id;
            targetGroupName = first.Name;
        }

        if (targetGroupId == Guid.Empty)
        {
            DbgJumpListStatusText.Text = "Please create at least one app group first.";
            DbgJumpListStatusText.Visibility = Visibility.Visible;
            return;
        }

        bool success = JumpListService.AddTestJumpListToGroup(targetGroupId);
        if (success)
        {
            DbgJumpListStatusText.Text = $"✔ Test jumplist added to '{targetGroupName}'! Right-click taskbar pin to view.";
            DbgJumpListStatusText.Visibility = Visibility.Visible;
        }
        else
        {
            DbgJumpListStatusText.Text = "Failed to update jumplist.";
            DbgJumpListStatusText.Visibility = Visibility.Visible;
        }
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
                ("Right",  "\uE76C", Models.LaunchSide.Right,  1, 1, true)
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
        var (major, minor, patch, fullVer) = DebugDiagnosticService.GetVersionInfo();
        var s = SettingsService.Current;
        var text = string.Join("\n", new[]
        {
            $"Version: {fullVer} (Major: {major}, Minor: {minor}, Patch: {patch})",
            $"Git: {DebugDiagnosticService.GetGitBranchAndCommit()}",
            $"Changes since 0.7: {DebugDiagnosticService.GetChangesSince07()}",
            $"GitHub: {DebugDiagnosticService.GetGitHubPushStatus()}",
            $"Working Tree: {DebugDiagnosticService.GetWorkingTreeStatus()}",
            $"Hotkey: {(HotkeyService.Instance.IsHookActive ? "Active" : "Inactive")}",
            $"Backdrop: {s.BackdropStyle}, Theme: {s.Theme}, Side: {s.LaunchSide}",
            $"Groups: {GroupService.Instance.Groups.Count}, FakeAppsRunning: {AppRunningService.FakeAppsRunningEnabled}",
            $"Detection: {BarDetectionService.Detect().Detail}"
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

    // ─── DTM Badge Tester ──────────────────────────────────────────────────────
    private string? _dbgCurrentBadgeApp = null;

    private void DbgBadgeAppCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DbgBadgeAppCombo.SelectedItem is ComboBoxItem item)
        {
            _dbgCurrentBadgeApp = item.Tag as string;
            DbgBadgeCountSlider.IsEnabled = true;

            // Restore existing mock count for this app
            int existing = TaskTile.Services.NotificationBadgeService.GetMockCount(_dbgCurrentBadgeApp ?? "");
            DbgBadgeCountSlider.Value = existing;
            DbgBadgeCountText.Text = existing.ToString();
        }
        else
        {
            _dbgCurrentBadgeApp = null;
            DbgBadgeCountSlider.IsEnabled = false;
            DbgBadgeCountSlider.Value = 0;
            DbgBadgeCountText.Text = "0";
            if (DbgBadgeStatusText != null) DbgBadgeStatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void DbgBadgeCountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        int count = (int)Math.Round(e.NewValue);
        if (DbgBadgeCountText != null) DbgBadgeCountText.Text = count.ToString();

        if (!string.IsNullOrEmpty(_dbgCurrentBadgeApp))
        {
            TaskTile.Services.NotificationBadgeService.SetMockCount(_dbgCurrentBadgeApp, count);

            if (DbgBadgeStatusText != null)
            {
                if (count > 0)
                {
                    DbgBadgeStatusText.Text = $"mock  {_dbgCurrentBadgeApp} → {count}";
                    DbgBadgeStatusText.Visibility = Visibility.Visible;
                }
                else
                {
                    DbgBadgeStatusText.Text = $"mock  {_dbgCurrentBadgeApp} cleared";
                    DbgBadgeStatusText.Visibility = Visibility.Visible;
                }
            }
        }
    }

    private void DbgBadgeClearAll_Click(object sender, RoutedEventArgs e)
    {
        // Clear all mock badges by setting each known name to 0
        foreach (string name in new[] { "WhatsApp", "Discord", "Telegram", "Outlook", "Mail", "Slack" })
        {
            TaskTile.Services.NotificationBadgeService.SetMockCount(name, 0);
        }
        // Also clear whatever is currently selected
        if (!string.IsNullOrEmpty(_dbgCurrentBadgeApp))
            TaskTile.Services.NotificationBadgeService.SetMockCount(_dbgCurrentBadgeApp, 0);

        DbgBadgeCountSlider.Value = 0;
        if (DbgBadgeCountText != null) DbgBadgeCountText.Text = "0";
        if (DbgBadgeStatusText != null)
        {
            DbgBadgeStatusText.Text = "all mock badges cleared";
            DbgBadgeStatusText.Visibility = Visibility.Visible;
        }
    }
}
