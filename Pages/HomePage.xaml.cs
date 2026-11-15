using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using TaskTile.Services;

namespace TaskTile.Pages;

public sealed partial class HomePage : Page
{
    private Compositor? _compositor;
    private record TipOrFact(bool IsTip, string Title, string Content);

    private readonly List<TipOrFact> _tipsAndFacts = new()
    {
        new(false, "Fun Fact", "My vision for the first ever prototype build of TaskTile wasn't gonna be like how it came, but instead would have \"Niches\" instead of app groups and would look like the Vistor ID style."),
        new(false, "Fun Fact", "TaskTile actually rhymes with \"Tactile\", now that was necessary to point out."),
        new(false, "Fun Fact", "While making the presentation image, i actually used a drawing app called IbisPaint."),
        new(false, "Fun Fact", "Most of the cool animations you see here, are animated \U0001f60e."),
        new(false, "Fun Fact", "TaskTile is 100% native using WinUI3 and C#, meaning it will sync well with your Windows 11 PC."),
        new(false, "Fun Fact", "Did you know? Every version of TaskTile had code-names, like the version your using right now is code-named \"shifu\"!"),
        new(true,  "Tip", "Your most used group is just one click away from the tray."),
        new(true,  "Tip", "You can hold on the settings icon with your mouse and flick it to rotate it."),
        new(true,  "Tip", "No need to click every icon in the aisle, just shift-select the pack inside.")
        new(true,  "shifu", "po, you are a code master, if you actually compiled the app but i forgot to add the instructions on how to, you  are master.")
    };

    // Dismiss preference keys — stored as plain text files in AppData\Local\TaskTile
    private static readonly string _settingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskTile");
    private const string KeyDismissedForever = "FunFact_DismissedForever";
    private const string KeyDismissedToday   = "FunFact_DismissedDate";
    private static bool _suppressBannerThisSession = false;

    private static string? ReadSetting(string key)
    {
        try
        {
            var path = Path.Combine(_settingsFolder, key + ".txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        } catch { return null; }
    }

    private static void WriteSetting(string key, string value)
    {
        try
        {
            Directory.CreateDirectory(_settingsFolder);
            File.WriteAllText(Path.Combine(_settingsFolder, key + ".txt"), value);
        } catch { }
    }

    public HomePage()
    {
        this.InitializeComponent();
        this.Loaded += HomePage_Loaded;
        GroupService.Instance.Groups.CollectionChanged += (_, _) => RefreshStats();
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        RefreshStats();
        ApplyBannerVisibility();
        
        // Show changelog on version upgrade
        _ = ShowChangelogIfNeededAsync();

        SetupCardVisual(GroupsCard);
        SetupCardVisual(AppsCard);
        SetupCardVisual(PinnedCard);
    }

    private const string CurrentVersion = "0.7.5";
    private const string VersionName = "Refreshes and just freshes!";

    private async Task ShowChangelogIfNeededAsync()
    {
        var s = TaskTile.Services.SettingsService.Current;
        if (s.LastSeenVersion == CurrentVersion) return;

        // Wait a bit so page animates in first
        await Task.Delay(600);

        this.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var changelogPanel = new StackPanel { Spacing = 8 };

                var res = Application.Current.Resources;
                var accentText = res.TryGetValue("AccentTextFillColorPrimaryBrush", out var b3) ? (Microsoft.UI.Xaml.Media.Brush)b3 : GetAccentBrush();
                var textSecondary = res.TryGetValue("TextFillColorSecondaryBrush", out var b4) ? (Microsoft.UI.Xaml.Media.Brush)b4 : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                var cardBg = res.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bCard) ? (Microsoft.UI.Xaml.Media.Brush)bCard : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(12, 255, 255, 255));
                var cardStroke = res.TryGetValue("CardStrokeColorDefaultBrush", out var bStroke) ? (Microsoft.UI.Xaml.Media.Brush)bStroke : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(16, 255, 255, 255));
                var badgeBg = res.TryGetValue("SubtleFillColorSecondaryBrush", out var bBadge) ? (Microsoft.UI.Xaml.Media.Brush)bBadge : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255));

                var badge = new Border
                {
                    Background = badgeBg,
                    BorderBrush = cardStroke,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 5, 12, 5),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 6),
                    Child = new TextBlock
                    {
                        Text = $"v{CurrentVersion} — {VersionName}",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = accentText,
                        FontSize = 12
                    }
                };
                changelogPanel.Children.Add(badge);

                var changes = new (string icon, string title, string desc)[]
                {
                    ("\uE74C", "Visitor ID & Niches", "Authentic Visitor ID card layout with 1:1 square icon plates, zero dead space, and optional Niches terminology."),
                    ("\uEE94", "Radial Menu & Gestures", "Spawns a circular menu orbiting around your cursor with double-press train launch and in-situ edit mode."),
                    ("\uE8D8", "Tactile Spring Bounce", "Cards spring bounce in anticipation then organically wiggle when entering edit mode with drag reordering."),
                    ("\uE713", "Fluent 2 Quick Settings", "Redesigned card settings with native grouped rows, instant theme toggle, and right-aligned controls."),
                    ("\uE8B7", "Dynamic Folder Support", "Full file & subfolder navigation with authentic high-res shell icon extraction."),
                    ("\uE718", "Polite Taskbar & Start", "Desktop & Start menu shortcuts with browser-style consent and system policy diagnostics."),
                    ("\uE790", "Performance & 60Hz Animations", "Smooth Fluent 2D slide-and-fade transitions, zero-flicker popup cache, and instant load times.")
                };

                foreach (var (icon, title, desc) in changes)
                {
                    var card = new Border
                    {
                        Background = cardBg,
                        BorderBrush = cardStroke,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(12, 10, 12, 10)
                    };

                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var iconBadge = new Border
                    {
                        Width = 32,
                        Height = 32,
                        CornerRadius = new CornerRadius(6),
                        Background = badgeBg,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Child = new FontIcon
                        {
                            Glyph = icon,
                            FontSize = 14,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                            Foreground = accentText,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    Grid.SetColumn(iconBadge, 0);

                    var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                    textStack.Children.Add(new TextBlock
                    {
                        Text = title,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 13
                    });
                    textStack.Children.Add(new TextBlock
                    {
                        Text = desc,
                        FontSize = 12,
                        Foreground = textSecondary,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords
                    });
                    Grid.SetColumn(textStack, 1);

                    row.Children.Add(iconBadge);
                    row.Children.Add(textStack);
                    card.Child = row;
                    changelogPanel.Children.Add(card);
                }

                var dialog = new ContentDialog
                {
                    Title = "What's new in TaskTile",
                    PrimaryButtonText = "Got it!",
                    XamlRoot = this.XamlRoot,
                    Content = new ScrollViewer
                    {
                        MaxHeight = 440,
                        Content = changelogPanel,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Padding = new Thickness(0, 4, 10, 0)
                    }
                };

                await dialog.ShowAsync();
                
                s.LastSeenVersion = CurrentVersion;
                TaskTile.Services.SettingsService.Save();
            }
            catch { }
        });
    }

    // ----- Fun Fact banner -----

    private void ApplyBannerVisibility()
    {
        if (_suppressBannerThisSession)
        {
            GettingStartedBanner.Visibility = Visibility.Collapsed;
            return;
        }

        if (ReadSetting(KeyDismissedForever) == "true")
        {
            GettingStartedBanner.Visibility = Visibility.Collapsed;
            return;
        }

        if (ReadSetting(KeyDismissedToday) == DateTime.Today.ToString("yyyy-MM-dd"))
        {
            GettingStartedBanner.Visibility = Visibility.Collapsed;
            return;
        }

        PickFunFact();
    }

    private int _currentTipIndex = -1;

    private void PickFunFact()
    {
        if (_tipsAndFacts.Count == 0) return;
        int nextIndex;
        do
        {
            nextIndex = new Random().Next(_tipsAndFacts.Count);
        } while (_tipsAndFacts.Count > 1 && nextIndex == _currentTipIndex);
        _currentTipIndex = nextIndex;

        var item = _tipsAndFacts[_currentTipIndex];
        BannerTitleText.Text = item.Title;
        BannerMessageText.Text = item.Content;

        if (item.IsTip)
        {
            // Blue icon for Tips (Image 2 style: icon itself is blue, no circle bg)
            BannerIcon.Glyph = "\uF167";
            BannerIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
        }
        else
        {
            // Yellow/amber icon for Fun Facts (Image 2 style: icon itself is yellow, no circle bg)
            BannerIcon.Glyph = "\uEA80";
            BannerIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 185, 0));
        }

        GettingStartedBanner.Visibility = Visibility.Visible;
    }

    private void RefreshTipButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var iconVisual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(RefreshTipIcon);
            var comp = iconVisual.Compositor;
            iconVisual.CenterPoint = new System.Numerics.Vector3(6f, 6f, 0f);
            var spinAnim = comp.CreateScalarKeyFrameAnimation();
            spinAnim.InsertKeyFrame(0.0f, 0f);
            spinAnim.InsertKeyFrame(1.0f, 360f);
            spinAnim.Duration = TimeSpan.FromMilliseconds(300);
            iconVisual.StartAnimation("RotationAngleInDegrees", spinAnim);
        }
        catch { }

        bool animationsEnabled = true;
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            animationsEnabled = uiSettings.AnimationsEnabled && !SettingsService.Current.DisableAnimation;
        }
        catch { }

        if (!animationsEnabled)
        {
            PickFunFact();
            return;
        }

        try
        {
            if (BannerContentGrid.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                BannerContentGrid.RenderTransform = tt;
            }

            var sbOut = new Storyboard();

            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, BannerContentGrid);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");

            var slideOut = new DoubleAnimation
            {
                To = -8.0,
                Duration = TimeSpan.FromMilliseconds(120),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slideOut, tt);
            Storyboard.SetTargetProperty(slideOut, "Y");

            sbOut.Children.Add(fadeOut);
            sbOut.Children.Add(slideOut);

            sbOut.Completed += (s, ev) =>
            {
                PickFunFact();
                tt.Y = 8.0;

                var sbIn = new Storyboard();

                var fadeIn = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(160),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, BannerContentGrid);
                Storyboard.SetTargetProperty(fadeIn, "Opacity");

                var slideIn = new DoubleAnimation
                {
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(160),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(slideIn, tt);
                Storyboard.SetTargetProperty(slideIn, "Y");

                sbIn.Children.Add(fadeIn);
                sbIn.Children.Add(slideIn);

                sbIn.Completed += (s2, ev2) =>
                {
                    BannerContentGrid.Opacity = 1.0;
                    tt.Y = 0.0;
                };

                sbIn.Begin();
            };

            sbOut.Begin();
        }
        catch
        {
            PickFunFact();
        }
    }

    private void DismissForever_Click(object sender, RoutedEventArgs e)
    {
        WriteSetting(KeyDismissedForever, "true");
        GettingStartedBanner.Visibility = Visibility.Collapsed;
        DismissFlyout.Hide();
    }

    private void DismissToday_Click(object sender, RoutedEventArgs e)
    {
        WriteSetting(KeyDismissedToday, DateTime.Today.ToString("yyyy-MM-dd"));
        GettingStartedBanner.Visibility = Visibility.Collapsed;
        DismissFlyout.Hide();
    }

    private void DismissOnce_Click(object sender, RoutedEventArgs e)
    {
        _suppressBannerThisSession = true;
        GettingStartedBanner.Visibility = Visibility.Collapsed;
        DismissFlyout.Hide();
    }

    private void CloseFlyout_Click(object sender, RoutedEventArgs e)
    {
        DismissFlyout.Hide();
    }

    // ----- Home page cards -----

    private void SetupCardVisual(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var size = ((FrameworkElement)element).ActualSize;
        visual.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);
    }

    private void RefreshStats(object sender, RoutedEventArgs e) => RefreshStats();

    private void RefreshStats()
    {
        var groups = GroupService.Instance.Groups;
        TotalGroupsText.Text  = groups.Count.ToString();
        TotalAppsText.Text    = groups.Sum(g => g.Apps.Count).ToString();
        PinnedGroupsText.Text = groups.Count(g => g.IsPinned).ToString();
    }

    // Map card border -> its FontIcon for accent-color swap
    private FontIcon? GetCardIcon(FrameworkElement card)
    {
        if (card == GroupsCard) return GroupsIcon;
        if (card == AppsCard)   return AppsIcon;
        if (card == PinnedCard) return PinnedIcon;
        return null;
    }

    private Brush? _accentBrush;
    private Brush GetAccentBrush()
    {
        if (_accentBrush != null) return _accentBrush;
        if (Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out var b) && b is Brush brush)
            _accentBrush = brush;
        else
            _accentBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
        return _accentBrush;
    }

    private Brush _dimBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 160));

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement card && _compositor != null)
        {
            // Scale up hovered, subtle dim others
            AnimateCard(card, 1.04f, 1.0f);
            if (card != GroupsCard) AnimateCard(GroupsCard, 1.0f, 0.82f);
            if (card != AppsCard)   AnimateCard(AppsCard,   1.0f, 0.82f);
            if (card != PinnedCard) AnimateCard(PinnedCard, 1.0f, 0.82f);

            // Accent-color icon on hovered card
            if (GetCardIcon(card) is FontIcon icon && GetAccentBrush() is SolidColorBrush sb)
                AnimateIconColor(icon, sb.Color);
        }
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateCard(GroupsCard, 1.0f, 1.0f);
        AnimateCard(AppsCard,   1.0f, 1.0f);
        AnimateCard(PinnedCard, 1.0f, 1.0f);

        // Reset all icons to secondary
        var secondaryBrush = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        if (secondaryBrush is SolidColorBrush sb)
        {
            AnimateIconColor(GroupsIcon, sb.Color);
            AnimateIconColor(AppsIcon, sb.Color);
            AnimateIconColor(PinnedIcon, sb.Color);
        }
    }

    private void AnimateIconColor(FontIcon icon, Windows.UI.Color targetColor)
    {
        var currentBrush = icon.Foreground as SolidColorBrush;
        var startColor = currentBrush?.Color ?? Microsoft.UI.Colors.Transparent;
        
        var animBrush = new SolidColorBrush(startColor);
        icon.Foreground = animBrush;
        
        var anim = new Microsoft.UI.Xaml.Media.Animation.ColorAnimation
        {
            To = targetColor,
            Duration = new Duration(TimeSpan.FromSeconds(0.25))
        };
        
        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, animBrush);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Color");
        storyboard.Children.Add(anim);
        storyboard.Begin();
    }

    private void AnimateCard(FrameworkElement card, float scale, float opacity)
    {
        var visual = ElementCompositionPreview.GetElementVisual(card);
        if (_compositor == null) return;

        var size = card.ActualSize;
        visual.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);

        var spring = _compositor.CreateSpringVector3Animation();
        spring.Target       = "Scale";
        spring.FinalValue   = new Vector3(scale, scale, 1.0f);
        spring.DampingRatio = 0.65f;
        spring.Period       = TimeSpan.FromSeconds(0.10);
        visual.StartAnimation("Scale", spring);

        var opacityAnim = _compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1.0f, opacity);
        opacityAnim.Duration = TimeSpan.FromSeconds(0.25); // smoother
        visual.StartAnimation("Opacity", opacityAnim);
    }

    // ----- Navigation taps -----

    private void GroupsCard_Tapped(object sender, TappedRoutedEventArgs e)
        => App.MainWindowInstance?.NavigateTo(typeof(GroupsPage));

    private bool _isDialogOpen = false;

    private async void AppsCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_isDialogOpen) return;
        _isDialogOpen = true;
        try
        {
            var groups = GroupService.Instance.Groups;
            if (!groups.Any())
            {
                var empty = new ContentDialog
                {
                    Title           = "No apps yet",
                    Content         = "Add apps to your groups first.",
                    CloseButtonText = "OK",
                    XamlRoot        = this.XamlRoot
                };
                await empty.ShowAsync();
                return;
            }

            // Build grouped list view
            var panel = new StackPanel { Spacing = 16 };
            foreach (var group in groups.Where(g => g.Apps.Count > 0))
            {
                // Group header
                var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 0, 0, 6) };
                header.Children.Add(new FontIcon
                {
                    Glyph      = "\uF168",
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    FontSize   = 16,
                    Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                });
                header.Children.Add(new TextBlock
                {
                    Text       = group.Name,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize   = 14
                });
                panel.Children.Add(header);

                // App entries
                foreach (var app in group.Apps)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(26, 0, 0, 4) };
                    row.Children.Add(new FontIcon
                    {
                        Glyph    = "\uE8A5",
                        FontSize = 12,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text       = app.Name,
                        FontSize   = 13,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
                    });
                    panel.Children.Add(row);
                }

                // Divider between groups
                if (group != groups.Last(g => g.Apps.Count > 0))
                    panel.Children.Add(new Border
                    {
                        Height     = 1,
                        Margin     = new Thickness(0, 4, 0, 0),
                        Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
                    });
            }

            var dialog = new ContentDialog
            {
                Title          = "Grouped Apps",
                CloseButtonText = "Close",
                XamlRoot       = this.XamlRoot,
                Content        = new ScrollViewer
                {
                    MaxHeight          = 400,
                    Content            = panel,
                    VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                }
            };
            await dialog.ShowAsync();
        }
        finally
        {
            _isDialogOpen = false;
        }
    }
}
