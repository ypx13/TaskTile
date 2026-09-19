using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskTile.Controls;
using TaskTile.Models;
using TaskTile.Services;

namespace TaskTile.Pages;

public sealed partial class GroupsPage : Page
{
    public GroupsPage()
    {
        this.InitializeComponent();

        GroupsItemsView.Layout = new UniformGridLayout
        {
            MinItemWidth     = 240,
            MinItemHeight    = 170,
            MaximumRowsOrColumns = 4,
            ItemsStretch     = UniformGridLayoutItemsStretch.Fill,
            MinColumnSpacing = 14,
            MinRowSpacing    = 14
        };
        GroupsItemsView.ItemsSource = GroupService.Instance.Groups;

        GroupCard.OpenFullSettingsRequested += OnOpenFullSettingsRequested;

        this.Loaded += (_, _) => RefreshGroups();
        GroupService.Instance.Groups.CollectionChanged += (_, _) => RefreshGroups();
    }

    private AppGroup? _currentSettingsGroup;
    private bool _isUpdatingFullUI = false;

    private void OnOpenFullSettingsRequested(GroupCard card, AppGroup group)
    {
        _currentSettingsGroup = group;
        PopulateFullSettings(group);

        if (SettingsService.Current.YpxMixUI)
        {
            Play3DFlipTransition(GroupsListRoot, FullGroupSettingsOverlay, forward: true);
        }
        else
        {
            GroupsListRoot.Visibility = Visibility.Collapsed;
            FullGroupSettingsOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CloseFullSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Current.YpxMixUI)
        {
            Play3DFlipTransition(FullGroupSettingsOverlay, GroupsListRoot, forward: false);
        }
        else
        {
            FullGroupSettingsOverlay.Visibility = Visibility.Collapsed;
            GroupsListRoot.Visibility = Visibility.Visible;
        }
        RefreshGroups();
    }

    private void Play3DFlipTransition(FrameworkElement fromElement, FrameworkElement toElement, bool forward)
    {
        var projFrom = new PlaneProjection { CenterOfRotationX = 0.5, CenterOfRotationY = 0.5 };
        var projTo = new PlaneProjection { CenterOfRotationX = 0.5, CenterOfRotationY = 0.5 };
        fromElement.Projection = projFrom;
        toElement.Projection = projTo;

        var sbOut = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var daOut = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = forward ? 90 : -90,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daOut, projFrom);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daOut, "RotationY");
        sbOut.Children.Add(daOut);

        sbOut.Completed += (s, ev) =>
        {
            fromElement.Visibility = Visibility.Collapsed;
            projFrom.RotationY = 0;

            toElement.Visibility = Visibility.Visible;
            projTo.RotationY = forward ? -90 : 90;

            var sbIn = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var daIn = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = forward ? -90 : 90,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(160)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daIn, projTo);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daIn, "RotationY");
            sbIn.Children.Add(daIn);
            sbIn.Completed += (_, _) => { toElement.Projection = null; fromElement.Projection = null; };
            sbIn.Begin();
        };

        sbOut.Begin();
    }

    private void PopulateFullSettings(AppGroup group)
    {
        _isUpdatingFullUI = true;

        FullSettingsGroupName.Text = string.IsNullOrEmpty(group.Name) ? "Group Settings" : $"Group Settings — {group.Name}";

        FullPopupStyleCombo.SelectedIndex = group.PopupStyle;
        FullHideNameToggle.IsOn = group.HideName;
        FullHideAppLabelsToggle.IsOn = group.HideAppLabels;
        FullMarqueeAppLabelsToggle.IsOn = group.MarqueeAppLabels;
        FullScrollAppLabelsToggle.IsOn = group.ScrollAppLabels;
        FullShowCardLabelsToggle.IsOn = group.ShowCardLabels;
        FullOverrideLaunchSideToggle.IsOn = group.OverrideLaunchSide;
        FullLaunchPositionCombo.SelectedIndex = group.GroupLaunchSide;
        FullDisableAnimationToggle.IsOn = group.DisableAnimation;
        FullDisableFloatToggle.IsOn = group.DisableFloat;
        FullDisableRoundedCornersToggle.IsOn = group.DisableRoundedCorners;
        FullDisableAutoHideToggle.IsOn = group.DisableAutoHide;
        FullMakeMainFocusToggle.IsOn = group.MakeMainFocus;
        FullKeepOpenToggle.IsOn = group.KeepOpen;
        FullDesktopModeToggle.IsOn = group.IsDesktopMode;
        FullPopupBackdropCombo.SelectedIndex = group.BackdropStyle;
        FullAlignmentCombo.SelectedIndex = group.CompactAlignment;
        FullAppIconStyleCombo.SelectedIndex = group.AppIconStyle;
        FullThemeOverrideCombo.SelectedIndex = group.ThemeOverride;
        FullTitleAlignmentSlider.Value = group.TitleAlignment;
        FullColumnsBox.Value = group.GridColumns;
        FullRowsBox.Value = group.GridRows;

        FullTaskbarOffsetSlider.Value = group.TaskbarOffset;
        FullTaskbarOffsetValText.Text = $"{group.TaskbarOffset}px";
        FullTileSpacingSlider.Value = group.TileSpacing;
        FullTileSpacingValText.Text = $"{group.TileSpacing}px";

        UpdateFullVisibility();
        _isUpdatingFullUI = false;
    }

    private void UpdateFullVisibility()
    {
        if (_currentSettingsGroup == null) return;
        bool isCompact = FullPopupStyleCombo.SelectedIndex == 1;
        FullAlignmentCombo.Visibility = isCompact ? Visibility.Visible : Visibility.Collapsed;

        bool isDialog = FullPopupStyleCombo.SelectedIndex == 4;
        FullShowCardLabelsToggle.Visibility = isDialog ? Visibility.Visible : Visibility.Collapsed;

        bool showOverrides = FullOverrideLaunchSideToggle.IsOn;
        FullLaunchPositionCombo.Visibility = showOverrides ? Visibility.Visible : Visibility.Collapsed;
        FullMakeMainFocusToggle.Visibility = (showOverrides && FullLaunchPositionCombo.SelectedIndex == 4) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FullSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;

        _currentSettingsGroup.PopupStyle = FullPopupStyleCombo.SelectedIndex;
        _currentSettingsGroup.HideName = FullHideNameToggle.IsOn;
        _currentSettingsGroup.HideAppLabels = FullHideAppLabelsToggle.IsOn;
        _currentSettingsGroup.MarqueeAppLabels = FullMarqueeAppLabelsToggle.IsOn;
        _currentSettingsGroup.ScrollAppLabels = FullScrollAppLabelsToggle.IsOn;
        _currentSettingsGroup.ShowCardLabels = FullShowCardLabelsToggle.IsOn;
        _currentSettingsGroup.OverrideLaunchSide = FullOverrideLaunchSideToggle.IsOn;
        _currentSettingsGroup.GroupLaunchSide = FullLaunchPositionCombo.SelectedIndex;
        _currentSettingsGroup.DisableAnimation = FullDisableAnimationToggle.IsOn;
        _currentSettingsGroup.DisableFloat = FullDisableFloatToggle.IsOn;
        _currentSettingsGroup.DisableRoundedCorners = FullDisableRoundedCornersToggle.IsOn;
        _currentSettingsGroup.DisableAutoHide = FullDisableAutoHideToggle.IsOn;
        _currentSettingsGroup.MakeMainFocus = FullMakeMainFocusToggle.IsOn;
        _currentSettingsGroup.KeepOpen = FullKeepOpenToggle.IsOn;
        _currentSettingsGroup.IsDesktopMode = FullDesktopModeToggle.IsOn;
        _currentSettingsGroup.BackdropStyle = FullPopupBackdropCombo.SelectedIndex;
        _currentSettingsGroup.CompactAlignment = FullAlignmentCombo.SelectedIndex;
        _currentSettingsGroup.AppIconStyle = FullAppIconStyleCombo.SelectedIndex;
        _currentSettingsGroup.ThemeOverride = FullThemeOverrideCombo.SelectedIndex;
        _currentSettingsGroup.TitleAlignment = (int)FullTitleAlignmentSlider.Value;

        GroupService.Instance.Save();
        UpdateFullVisibility();
        if (_currentSettingsGroup.IsPinned) TaskbarService.PinGroup(_currentSettingsGroup);
    }

    private void FullGapSettingChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        _currentSettingsGroup.TaskbarOffset = (int)FullTaskbarOffsetSlider.Value;
        FullTaskbarOffsetValText.Text = $"{_currentSettingsGroup.TaskbarOffset}px";
        _currentSettingsGroup.TileSpacing = (int)FullTileSpacingSlider.Value;
        FullTileSpacingValText.Text = $"{_currentSettingsGroup.TileSpacing}px";
        GroupService.Instance.Save();
    }

    private void FullNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        _currentSettingsGroup.GridColumns = (int)FullColumnsBox.Value;
        _currentSettingsGroup.GridRows = (int)FullRowsBox.Value;
        GroupService.Instance.Save();
    }

    private async void FullDeleteGroupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;

        var dialog = new ContentDialog
        {
            Title = $"Delete \"{_currentSettingsGroup.Name}\"?",
            Content = "Are you sure you want to delete this group? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (_currentSettingsGroup.IsPinned) TaskbarService.UnpinGroup(_currentSettingsGroup);
            GroupService.Instance.RemoveGroup(_currentSettingsGroup.Id);
            CloseFullSettingsBtn_Click(sender, e);
        }
    }

    private void RefreshGroups()
    {
        var groups = GroupService.Instance.Groups;
        EmptyState.Visibility = groups.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        // ── Step 1: ask Apps or Files? ────────────────────────────────────────
        var typeDialog = new ContentDialog
        {
            Title         = "What kind of group?",
            CloseButtonText = "Cancel",
            XamlRoot      = XamlRoot
        };

        var appsBtn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 16, 16, 16),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var filesBtn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 16, 16, 16),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255))
        };

        // Apps card content
        var appsIcon = new FontIcon { Glyph = "\uECAA", FontSize = 32, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5), RenderTransform = new Microsoft.UI.Xaml.Media.RotateTransform() };
        var appsTextPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        appsTextPanel.Children.Add(new TextBlock { Text = "Apps", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        appsTextPanel.Children.Add(new TextBlock { Text = "Launch apps from a popup", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var appsGrid = new Grid();
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(appsIcon, 0); Grid.SetColumn(appsTextPanel, 1);
        appsGrid.Children.Add(appsIcon); appsGrid.Children.Add(appsTextPanel);
        appsBtn.Content = appsGrid;

        // Apps animation
        appsBtn.PointerEntered += (_, _) =>
        {
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimationUsingKeyFrames();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, appsIcon.RenderTransform);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Angle");
            anim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(100), Value = -15 });
            anim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(200), Value = 15 });
            anim.KeyFrames.Add(new Microsoft.UI.Xaml.Media.Animation.EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(300), Value = 0 });
            sb.Children.Add(anim);
            sb.Begin();
        };
        appsBtn.PointerExited += (_, _) =>
        {
            if (appsIcon.RenderTransform is Microsoft.UI.Xaml.Media.RotateTransform rt) rt.Angle = 0;
        };

        // Files card content
        var filesIcon = new FontIcon { Glyph = "\uE8B7", FontSize = 32, VerticalAlignment = VerticalAlignment.Center };
        var filesTransformGroup = new Microsoft.UI.Xaml.Media.TransformGroup();
        filesTransformGroup.Children.Add(new Microsoft.UI.Xaml.Media.RotateTransform());
        filesTransformGroup.Children.Add(new Microsoft.UI.Xaml.Media.TranslateTransform());
        filesIcon.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        filesIcon.RenderTransform = filesTransformGroup;

        var filesTextPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        var filesTitleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        filesTitleRow.Children.Add(new TextBlock { Text = "Files", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        filesTitleRow.Children.Add(new Border { CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 180, 0)), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "Experimental", FontSize = 10, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 200, 50)) } });
        filesTextPanel.Children.Add(filesTitleRow);
        filesTextPanel.Children.Add(new TextBlock { Text = "Open files with their default apps", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var filesGrid = new Grid();
        filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(filesIcon, 0); Grid.SetColumn(filesTextPanel, 1);
        filesGrid.Children.Add(filesIcon); filesGrid.Children.Add(filesTextPanel);
        filesBtn.Content = filesGrid;

        // Files animation
        filesBtn.PointerEntered += (_, _) =>
        {
            filesIcon.Glyph = "\uE838"; // open folder
            var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var ease = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut };
            var rot = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = -10, Duration = new Duration(TimeSpan.FromMilliseconds(150)), AutoReverse = true, EasingFunction = ease };
            var trans = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation { To = -4, Duration = new Duration(TimeSpan.FromMilliseconds(150)), AutoReverse = true, EasingFunction = ease };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(rot, filesTransformGroup.Children[0]);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(rot, "Angle");
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(trans, filesTransformGroup.Children[1]);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(trans, "Y");
            sb.Children.Add(rot); sb.Children.Add(trans);
            sb.Begin();
        };
        filesBtn.PointerExited += (_, _) =>
        {
            filesIcon.Glyph = "\uE8B7"; // normal folder
            if (filesTransformGroup.Children[0] is Microsoft.UI.Xaml.Media.RotateTransform rt) rt.Angle = 0;
            if (filesTransformGroup.Children[1] is Microsoft.UI.Xaml.Media.TranslateTransform tt) tt.Y = 0;
        };

        var chosen = GroupType.Apps;
        bool buttonClicked = false;
        appsBtn.Click  += (_, _) => { chosen = GroupType.Apps;  buttonClicked = true; typeDialog.Hide(); };
        filesBtn.Click += (_, _) => { chosen = GroupType.Files; buttonClicked = true; typeDialog.Hide(); };

        typeDialog.Content = new StackPanel
        {
            Spacing = 12,
            Width   = 320,
            Children = { appsBtn, filesBtn }
        };

        await typeDialog.ShowAsync();
        if (!buttonClicked) return; // user hit Cancel

        // ── Step 2: open the appropriate creation dialog ──────────────────────
        if (chosen == GroupType.Files)
        {
            var fileDialog = new CreateFileGroupDialog { XamlRoot = XamlRoot };
            fileDialog.WithGeneratedTemplate();
            await fileDialog.ShowAsync();
            if (fileDialog.ResultGroup != null)
                GroupService.Instance.AddGroup(fileDialog.ResultGroup);
        }
        else
        {
            var appDialog = new CreateGroupDialog { XamlRoot = XamlRoot };
            var result = await appDialog.ShowAsync();
            if (appDialog.ResultGroup != null)
                GroupService.Instance.AddGroup(appDialog.ResultGroup);
        }
    }
}
