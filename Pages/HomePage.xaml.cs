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
using TaskTile.Services;

namespace TaskTile.Pages;

public sealed partial class HomePage : Page
{
    private Compositor? _compositor;
    private readonly List<string> _funFacts = new()
    {
        "TaskTile actually rhymes with \"Tactile\", now that was necessary to point out.",
        "Your most used group is just one click away from the tray.",
        "While making the presentation image, i actually used a drawing app called IbisPaint.",
        "Most of the cool animations you see here, are animated \U0001f60e.",
        "TaskTile is 100% native using WinUI3 and C#, meaning it will sync well with your Windows 11 PC.",
        "You can hold on the settings icon with your mouse and flick it to rotate it.",
        "No need to click every icon in the aisle, just shift-select the pack inside."
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

    private const string CurrentVersion = "0.7";
    private const string VersionName = "Borders, Corners, and alot of folders";

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
                var changelogPanel = new StackPanel { Spacing = 12 };

                var res = Application.Current.Resources;
                var accentBg = res.TryGetValue("AccentFillColorDefaultBrush", out var b1) ? (Microsoft.UI.Xaml.Media.Brush)b1 : GetAccentBrush();
                var textOnAccent = res.TryGetValue("TextOnAccentFillColorPrimaryBrush", out var b2) ? (Microsoft.UI.Xaml.Media.Brush)b2 : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                var accentText = res.TryGetValue("AccentTextFillColorPrimaryBrush", out var b3) ? (Microsoft.UI.Xaml.Media.Brush)b3 : GetAccentBrush();
                var textSecondary = res.TryGetValue("TextFillColorSecondaryBrush", out var b4) ? (Microsoft.UI.Xaml.Media.Brush)b4 : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

                var badge = new Border
                {
                    Background = accentBg,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = $"v{CurrentVersion} — {VersionName}",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = textOnAccent,
                        FontSize = 13
                    }
                };
                changelogPanel.Children.Add(badge);

                var changes = new (string icon, string title, string desc)[]
                {
                    ("\uE80F", "Home & Welcome", "Redesigned Home page and added a welcome for first time users."),
                    ("\uE81E", "Layouts Polished", "Polished Classic Grid, Compact, and List layouts. Added a new Dialog-ish style!"),
                    ("\uE70F", "In-App Editing", "Added editing directly from the app group pop-up 🧪 (Experimental)."),
                    ("\uE713", "Customization Galore", "Added global settings, title location in group settings, and a ton of customization options!"),
                    ("\uE8B8", "Acrylic & Visuals", "FIXED ACRYLIC 😭, added WinUI3 style hover, and a couple of animations here and there."),
                    ("\uE83B", "Folders & Groups", "Added Dynamic folder groups and manual positioning. Changed Group-settings from a dialog to a pop-up."),
                    ("\uE9D9", "Fixes & Goodies", "Fixed some bugs (group-gate), added 'other-taskbar' detection, delete data, fun-facts/tips, and a tray-icon.")
                };

                foreach (var (icon, title, desc) in changes)
                {
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var iconBlock = new FontIcon
                    {
                        Glyph = icon,
                        FontSize = 18,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                        Foreground = accentText,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    Grid.SetColumn(iconBlock, 0);

                    var textStack = new StackPanel { Spacing = 2 };
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
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    });
                    Grid.SetColumn(textStack, 1);

                    row.Children.Add(iconBlock);
                    row.Children.Add(textStack);
                    changelogPanel.Children.Add(row);
                }

                var dialog = new ContentDialog
                {
                    Title = "What's new in TaskTile",
                    PrimaryButtonText = "Got it!",
                    XamlRoot = this.XamlRoot,
                    Content = new ScrollViewer
                    {
                        MaxHeight = 420,
                        Content = changelogPanel,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Padding = new Thickness(0, 4, 12, 0)
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

    private void PickFunFact()
    {
        var tip = _funFacts[new Random().Next(_funFacts.Count)];
        BannerMessageRun.Text = tip;
        GettingStartedBanner.Visibility = Visibility.Visible;
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
