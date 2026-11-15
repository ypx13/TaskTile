using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using TaskTile.Controls;
using TaskTile.Models;
using TaskTile.Services;

namespace TaskTile.Pages;

public sealed partial class GroupsPage : Page
{
    public GroupsPage()
    {
        this.InitializeComponent();

        ApplyLayoutForCurrentStyle();
        GroupsItemsView.ItemsSource = GroupService.Instance.Groups;

        GroupCard.OpenFullSettingsRequested += OnOpenFullSettingsRequested;
        GroupCard.EditModeRequested += OnEditModeRequested;

        SetupFullTitleAlignmentPreview();

        this.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnGlobalPointerPressed), true);

        this.Loaded += (_, _) =>
        {
            ApplyLayoutForCurrentStyle();
            RefreshGroups();
            var items = GroupsItemsView.ItemsSource;
            GroupsItemsView.ItemsSource = null;
            GroupsItemsView.ItemsSource = items;
        };
        GroupService.Instance.Groups.CollectionChanged += (_, _) => RefreshGroups();
        SettingsService.SettingsChanged += OnSettingsChanged;
        this.Unloaded += (_, _) =>
        {
            this.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnGlobalPointerPressed));
            SettingsService.SettingsChanged -= OnSettingsChanged;
            GroupCard.EditModeRequested -= OnEditModeRequested;
        };

        // Radial menu option hidden - removed NewRadialGroupBtn_Click method and radial menu button
        // Backup settings hidden - removed EnableBackupSystemToggle functionality
    }

    private void OnGlobalPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        if (source is not TextBox)
        {
            GroupCard.CommitActiveRename();
        }
    }

    private void ApplyLayoutForCurrentStyle()
    {
        int style = SettingsService.Current.GroupCardStyle;
        if (GroupsItemsView.Layout is not UniformGridLayout layout)
        {
            layout = new UniformGridLayout();
            GroupsItemsView.Layout = layout;
        }

        layout.ItemsStretch = UniformGridLayoutItemsStretch.Fill;

        if (style == 2)
        {
            // Compact (formerly Super Compact): single line per group, horizontally fills the width, vertically short
            layout.MinItemWidth = 400;
            layout.MinItemHeight = 52;
            layout.MaximumRowsOrColumns = 1;
            layout.MinColumnSpacing = 0;
            layout.MinRowSpacing = 8;
        }
        else if (style == 1)
        {
            // Detailed (formerly Visitor ID): prominent square card
            layout.MinItemWidth = 190;
            layout.MinItemHeight = 215;
            layout.MaximumRowsOrColumns = 6;
            layout.MinColumnSpacing = 12;
            layout.MinRowSpacing = 12;
        }
        else
        {
            // Classic (formerly Default): horizontal card, chin eliminated
            layout.MinItemWidth = 240;
            layout.MinItemHeight = 130;
            layout.MaximumRowsOrColumns = 4;
            layout.MinColumnSpacing = 14;
            layout.MinRowSpacing = 14;
        }
    }

    private void OnEditModeRequested()
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            DoneReorderButton.Visibility = Visibility.Visible;
            GroupCard.SetGlobalEditMode(true);
        });
    }

    private void DoneReorderButton_Click(object sender, RoutedEventArgs e)
    {
        DoneReorderButton.Visibility = Visibility.Collapsed;
        GroupCard.SetGlobalEditMode(false);
        GroupService.Instance.Save();
    }

    private void OnSettingsChanged()
    {
        this.DispatcherQueue?.TryEnqueue(() =>
        {
            ApplyLayoutForCurrentStyle();
            RefreshGroups();
            var items = GroupsItemsView.ItemsSource;
            GroupsItemsView.ItemsSource = null;
            GroupsItemsView.ItemsSource = items;
        });
    }

    private AppGroup? _currentSettingsGroup;
    private bool _isUpdatingFullUI = false;

    public void OpenGroupFullSettings(Guid groupId)
    {
        var group = GroupService.Instance.Groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            _currentSettingsGroup = group;
            PopulateFullSettings(group);
            if (FullGroupSettingsOverlay.Visibility != Visibility.Visible)
            {
                PlayNormalFluentTransition(GroupsListRoot, FullGroupSettingsOverlay, forward: true);
            }
        }
    }

    private void OnOpenFullSettingsRequested(GroupCard card, AppGroup group)
    {
        _currentSettingsGroup = group;
        PopulateFullSettings(group);
        PlayNormalFluentTransition(GroupsListRoot, FullGroupSettingsOverlay, forward: true);
    }

    private void CloseFullSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        FadeOutFullPreview();
        PlayNormalFluentTransition(FullGroupSettingsOverlay, GroupsListRoot, forward: false);
        GroupsItemsView.ItemsSource = null;
        GroupsItemsView.ItemsSource = GroupService.Instance.Groups;
        RefreshGroups();
    }

    private void PlayNormalFluentTransition(FrameworkElement fromElement, FrameworkElement toElement, bool forward)
    {
        fromElement.Visibility = Visibility.Collapsed;
        toElement.Visibility = Visibility.Visible;

        var trans = new TranslateTransform { Y = forward ? 14 : -14 };
        toElement.RenderTransform = trans;
        toElement.Opacity = 0;

        var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var daFade = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(140)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daFade, toElement);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daFade, "Opacity");
        sb.Children.Add(daFade);

        var daSlide = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = forward ? 14 : -14,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(140)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daSlide, trans);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daSlide, "Y");
        sb.Children.Add(daSlide);

        sb.Completed += (_, _) =>
        {
            toElement.RenderTransform = null;
            toElement.Opacity = 1;
        };
        sb.Begin();
    }

    private void PopulateFullSettings(AppGroup group)
    {
        _isUpdatingFullUI = true;

        FullSettingsGroupNameBox.Text = group.Name ?? "";
        RefreshFullAppsList();

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
        FullRunningIndicatorStyleCombo.SelectedIndex = group.RunningIndicatorStyle == 0 ? 1 : (group.RunningIndicatorStyle == 1 ? 2 : 0);
        FullThemeOverrideCombo.SelectedIndex = group.ThemeOverride;
        int titleAlign = group.TitleAlignment < 0 ? SettingsService.Current.TitleAlignment : group.TitleAlignment;
        FullTitleAlignmentSlider.Value = Math.Clamp(titleAlign, 0, 2);
        FullTaskbarGapSlider.Value = group.TaskbarOffset;
        if (FullPreviewTitleText != null)
        {
            FullPreviewTitleText.Text = string.IsNullOrWhiteSpace(group.Name) ? "Sample" : group.Name;
        }
        UpdateFullPreviewTitlePosition((int)FullTitleAlignmentSlider.Value, animate: false);
        FullColumnsBox.Value = group.GridColumns;
        FullRowsBox.Value = group.GridRows;

        FullFolderIconStyleCombo.SelectedIndex = Math.Clamp(group.FolderIconStyle, 0, 2);
        FullMonochromeFolderIconToggle.IsOn = group.MonochromeFolderIcon;
        FullClearCustomIconButton.Visibility = (!string.IsNullOrEmpty(group.CustomIconPath) && System.IO.File.Exists(group.CustomIconPath))
            ? Visibility.Visible : Visibility.Collapsed;

        FullOverrideBorderColorToggle.IsOn = group.OverrideBorderColor;
        try
        {
            string hex = group.CustomBorderColor;
            if (!string.IsNullOrEmpty(hex) && hex.Length >= 7)
            {
                byte r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                FullCustomBorderColorPicker.Color = Windows.UI.Color.FromArgb(255, r, g, b);
            }
        }
        catch { }
        FullBorderColorButton.Visibility = group.OverrideBorderColor ? Visibility.Visible : Visibility.Collapsed;

        FullRadialShortcutBox.Text = group.Shortcut ?? "Ctrl+Shift+A";

        FullTileSpacingSlider.Value = group.TileSpacing > 0 ? group.TileSpacing : 8;
        FullTaskbarGapSlider.Value = group.TaskbarOffset;

        UpdateFullVisibility();
        _isUpdatingFullUI = false;
    }

    private void UpdateFullVisibility()
    {
        if (_currentSettingsGroup == null) return;

        bool isRadial = _currentSettingsGroup.GroupType == GroupType.Radial;
        FullRadialSettingsCard.Visibility = isRadial ? Visibility.Visible : Visibility.Collapsed;
        FullLayoutCard.Visibility = isRadial ? Visibility.Collapsed : Visibility.Visible;
        FullTitleLabelsCard.Visibility = isRadial ? Visibility.Collapsed : Visibility.Visible;
        FullBehaviorCard.Visibility = isRadial ? Visibility.Collapsed : Visibility.Visible;

        if (isRadial) return;

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
        _currentSettingsGroup.RunningIndicatorStyle = FullRunningIndicatorStyleCombo.SelectedIndex == 1 ? 0 : (FullRunningIndicatorStyleCombo.SelectedIndex == 2 ? 1 : -1);
        _currentSettingsGroup.ThemeOverride = FullThemeOverrideCombo.SelectedIndex;
        _currentSettingsGroup.TitleAlignment = (int)FullTitleAlignmentSlider.Value;
        _currentSettingsGroup.TaskbarOffset = (int)Math.Round(FullTaskbarGapSlider.Value);
        _currentSettingsGroup.TileSpacing = (int)Math.Round(FullTileSpacingSlider.Value);
        _currentSettingsGroup.FolderIconStyle = FullFolderIconStyleCombo.SelectedIndex;
        _currentSettingsGroup.MonochromeFolderIcon = FullMonochromeFolderIconToggle.IsOn;
        _currentSettingsGroup.OverrideBorderColor = FullOverrideBorderColorToggle.IsOn;
        FullBorderColorButton.Visibility = FullOverrideBorderColorToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;

        GroupService.Instance.Save();
        UpdateFullVisibility();
        if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
    }

    private void FullTaskbarGapSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        int val = (int)Math.Round(e.NewValue);
        _currentSettingsGroup.TaskbarOffset = val;
        GroupService.Instance.Save();
    }

    private void FullCustomBorderColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        var c = args.NewColor;
        _currentSettingsGroup.CustomBorderColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        GroupService.Instance.Save();
    }

    private void FullGapSettingChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        _currentSettingsGroup.TileSpacing = (int)Math.Round(FullTileSpacingSlider.Value);
        GroupService.Instance.Save();
    }

    private void FullNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        _currentSettingsGroup.GridColumns = (int)FullColumnsBox.Value;
        _currentSettingsGroup.GridRows = (int)FullRowsBox.Value;
        GroupService.Instance.Save();
    }

    private void RefreshFullAppsList()
    {
        if (_currentSettingsGroup != null)
        {
            foreach (var app in _currentSettingsGroup.Apps)
            {
                if (string.IsNullOrEmpty(app.IconPath) && !string.IsNullOrEmpty(app.ExePath))
                {
                    try
                    {
                        app.IconPath = IconHelper.GetOrExtractIcon(app.ExePath);
                    }
                    catch { }
                }
            }
            FullAppsListView.ItemsSource = null;
            FullAppsListView.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<AppEntry>(_currentSettingsGroup.Apps);
        }
    }

    private void FullSettingsGroupNameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        string newName = FullSettingsGroupNameBox.Text.Trim();
        if (!string.IsNullOrEmpty(newName) && _currentSettingsGroup.Name != newName)
        {
            _currentSettingsGroup.Name = newName;
            GroupService.Instance.Save();
            if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
        }

        if (FullPreviewTitleText != null)
        {
            FullPreviewTitleText.Text = string.IsNullOrWhiteSpace(newName) ? "Sample" : newName;
            UpdateFullPreviewTitlePosition((int)FullTitleAlignmentSlider.Value, animate: false);
        }
    }

    private void FullAddFile_Click(object sender, RoutedEventArgs e)
    {
        FullAddAppBtn_Click(sender, e);
    }

    private async void FullAddInstalledApp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;
        try
        {
            var searchBox = new TextBox { PlaceholderText = "Search apps...", Margin = new Thickness(0, 0, 0, 10) };
            var loading = new ProgressRing { IsActive = true, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 20) };
            var listView = new ListView
            {
                MaxHeight = 280,
                SelectionMode = ListViewSelectionMode.Multiple,
                Visibility = Visibility.Collapsed
            };

            string templateXaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Grid Padding=""4,3"" ColumnSpacing=""10"">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width=""Auto""/>
            <ColumnDefinition Width=""*""/>
        </Grid.ColumnDefinitions>
        <FontIcon Grid.Column=""0"" Glyph=""&#xE71D;"" FontSize=""14"" VerticalAlignment=""Center""/>
        <TextBlock Grid.Column=""1"" Text=""{Binding Name}"" VerticalAlignment=""Center"" TextTrimming=""CharacterEllipsis""/>
    </Grid>
</DataTemplate>";
            listView.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(templateXaml);

            var allApps = new List<AppEntry>();
            var displayApps = new System.Collections.ObjectModel.ObservableCollection<AppEntry>();
            listView.ItemsSource = displayApps;

            searchBox.TextChanged += (_, _) =>
            {
                displayApps.Clear();
                var q = searchBox.Text.Trim();
                var filtered = string.IsNullOrEmpty(q) ? allApps : allApps.Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
                foreach (var a in filtered) displayApps.Add(a);
            };

            var stack = new StackPanel { Width = 360, Children = { searchBox, loading, listView } };
            var dialog = new ContentDialog
            {
                Title = "Add Installed Apps",
                Content = stack,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            _ = System.Threading.Tasks.Task.Run(() => AppDiscoveryService.GetInstalledApps()).ContinueWith(t =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    allApps = t.Result;
                    foreach (var a in allApps) displayApps.Add(a);
                    loading.IsActive = false;
                    loading.Visibility = Visibility.Collapsed;
                    listView.Visibility = Visibility.Visible;
                });
            });

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                foreach (var item in listView.SelectedItems.OfType<AppEntry>())
                {
                    _currentSettingsGroup.Apps.Add(new AppEntry
                    {
                        Name = item.Name,
                        ExePath = item.ExePath,
                        IconPath = item.IconPath,
                        TileSize = 1
                    });
                }
                GroupService.Instance.Save();
                if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
                RefreshFullAppsList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding installed apps in full settings: {ex.Message}");
        }
    }

    private async void FullAddAppBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add(".lnk");
            picker.FileTypeFilter.Add(".url");
            picker.FileTypeFilter.Add(".bat");
            picker.FileTypeFilter.Add(".cmd");
            picker.FileTypeFilter.Add(".msi");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                string friendlyName = await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(file.Path);
                        if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
                        if (!string.IsNullOrWhiteSpace(info.FileDescription)) return info.FileDescription.Trim();
                    }
                    catch { }
                    return System.IO.Path.GetFileNameWithoutExtension(file.Path);
                });

                string iconPath = "";
                try { iconPath = IconHelper.GetOrExtractIcon(file.Path); } catch { }

                _currentSettingsGroup.Apps.Add(new AppEntry
                {
                    Name = friendlyName,
                    ExePath = file.Path,
                    IconPath = iconPath,
                    TileSize = 1
                });
                GroupService.Instance.Save();
                if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
                RefreshFullAppsList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding app in full settings: {ex.Message}");
        }
    }

    private void FullAppMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null || sender is not Button btn || btn.Tag is not AppEntry app) return;
        int idx = _currentSettingsGroup.Apps.IndexOf(app);
        if (idx > 0)
        {
            _currentSettingsGroup.Apps.RemoveAt(idx);
            _currentSettingsGroup.Apps.Insert(idx - 1, app);
            GroupService.Instance.Save();
            if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
            RefreshFullAppsList();
        }
    }

    private void FullAppMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null || sender is not Button btn || btn.Tag is not AppEntry app) return;
        int idx = _currentSettingsGroup.Apps.IndexOf(app);
        if (idx >= 0 && idx < _currentSettingsGroup.Apps.Count - 1)
        {
            _currentSettingsGroup.Apps.RemoveAt(idx);
            _currentSettingsGroup.Apps.Insert(idx + 1, app);
            GroupService.Instance.Save();
            if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
            RefreshFullAppsList();
        }
    }

    private void FullAppCycleSize_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null || sender is not Button btn || btn.Tag is not AppEntry app) return;
        app.TileSize = (app.TileSize % 4) + 1;
        GroupService.Instance.Save();
        RefreshFullAppsList();
    }

    private void FullAppDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null || sender is not Button btn || btn.Tag is not AppEntry app) return;
        _currentSettingsGroup.Apps.Remove(app);
        GroupService.Instance.Save();
        if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
        RefreshFullAppsList();
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

    private void FullRadialShortcutBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        _currentSettingsGroup.Shortcut = FullRadialShortcutBox.Text.Trim();
        GroupService.Instance.Save();
        HotkeyService.Instance.UpdateAllHotkeys();
    }

    private void FullTestRadialBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;
        RadialMenuManager.Show(_currentSettingsGroup);
    }

    private async void FullCustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;

        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".ico");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            _currentSettingsGroup.CustomIconPath = file.Path;
            FullClearCustomIconButton.Visibility = Visibility.Visible;
            if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
            GroupService.Instance.Save();
        }
    }

    private void FullClearCustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSettingsGroup == null) return;
        _currentSettingsGroup.CustomIconPath = string.Empty;
        FullClearCustomIconButton.Visibility = Visibility.Collapsed;
        if (_currentSettingsGroup.IsPinned) TaskbarService.UpdateGroupShortcut(_currentSettingsGroup);
        GroupService.Instance.Save();
    }

    private void RefreshGroups()
    {
        var groups = GroupService.Instance.Groups;
        EmptyState.Visibility = groups.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool useNiches = SettingsService.Current.UseNichesTerminology;
        PageTitleText.Text = useNiches ? "Niches" : "Groups";
        PageSubtitleText.Text = useNiches ? "Create and manage your niches." : "Create and manage your app groups.";
        EmptyStateTitleText.Text = useNiches ? "No niches yet" : "No groups yet";
        EmptyStateSubtitleText.Text = useNiches ? "Click the 'New niche' button below to create your first niche." : "Click the 'New group' button below to create your first group.";
        AddGroupButtonText.Text = useNiches ? "New niche" : "New group";
        ToolTipService.SetToolTip(AddGroupButton, useNiches ? "Create a new niche" : "Create a new group");
        FlyoutHeaderTitle.Text = useNiches ? "Create new niche" : "Create new group";

        ApplyLayoutForCurrentStyle();
    }

    private bool _isOpeningDialog = false;

    private async Task ShowContentDialogSafely(ContentDialog dialog)
    {
        if (_isOpeningDialog) return;
        _isOpeningDialog = true;
        try
        {
            dialog.XamlRoot = this.XamlRoot;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await dialog.ShowAsync();
                    break;
                }
                catch (System.Runtime.InteropServices.COMException ex) when ((uint)ex.HResult == 0x80000019)
                {
                    // If another dialog was closing, wait a moment and retry
                    await System.Threading.Tasks.Task.Delay(150);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GroupsPage] Dialog error: {ex}");
        }
        finally
        {
            _isOpeningDialog = false;
        }
    }

    private async void NewAppGroupBtn_Click(object sender, RoutedEventArgs e)
    {
        NewGroupFlyout?.Hide();
        var appDialog = new CreateGroupDialog { XamlRoot = this.XamlRoot };
        await ShowContentDialogSafely(appDialog);
        if (appDialog.ResultGroup != null)
            GroupService.Instance.AddGroup(appDialog.ResultGroup);
    }

    private async void NewFileGroupBtn_Click(object sender, RoutedEventArgs e)
    {
        NewGroupFlyout?.Hide();
        var fileDialog = new CreateFileGroupDialog { XamlRoot = this.XamlRoot };
        fileDialog.WithGeneratedTemplate();
        await ShowContentDialogSafely(fileDialog);
        if (fileDialog.ResultGroup != null)
            GroupService.Instance.AddGroup(fileDialog.ResultGroup);
    }

    private async void NewRadialGroupBtn_Click(object sender, RoutedEventArgs e)
    {
        NewGroupFlyout?.Hide();
        var radialDialog = new CreateGroupDialog
        {
            XamlRoot = this.XamlRoot,
            TargetGroupType = GroupType.Radial
        };
        await ShowContentDialogSafely(radialDialog);
        if (radialDialog.ResultGroup != null)
        {
            GroupService.Instance.AddGroup(radialDialog.ResultGroup);
            HotkeyService.Instance.UpdateAllHotkeys();
        }
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NewGroupFlyout?.ShowAt(AddGroupButton);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GroupsPage] AddGroupButton_Click error: {ex}");
        }
    }

    // ─── Fullscreen Title Alignment Preview ──────────────────────────────────────

    private DispatcherTimer? _fullPreviewHideTimer;
    private bool _fullPreviewVisible = false;
    private Storyboard? _fullPreviewOpacitySb;
    private const double FullPreviewContainerFullHeight = 48;

    private void SetupFullTitleAlignmentPreview()
    {
        if (FullPreviewTitleHost != null)
            FullPreviewTitleHost.SizeChanged += (s, e) => UpdateFullPreviewTitlePosition((int)FullTitleAlignmentSlider.Value, animate: false);

        if (FullTitleAlignmentSlider != null)
        {
            FullTitleAlignmentSlider.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((s, e) => ShowFullPreview()), true);
        }

        _fullPreviewHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1300) };
        _fullPreviewHideTimer.Tick += (s, e) =>
        {
            _fullPreviewHideTimer!.Stop();
            FadeOutFullPreview();
        };
    }

    private void ShowFullPreview()
    {
        // Suppressed per user request
    }

    private void FadeOutFullPreview()
    {
        if (FullTitlePreviewHost == null || FullTitlePreviewContainer == null || !_fullPreviewVisible) return;

        _fullPreviewOpacitySb?.Stop();

        var opacityAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = FullTitlePreviewHost.Opacity,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn }
        };
        _fullPreviewOpacitySb = new Storyboard();
        Storyboard.SetTarget(opacityAnim, FullTitlePreviewHost);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        _fullPreviewOpacitySb.Children.Add(opacityAnim);
        _fullPreviewOpacitySb.Completed += (s, e) =>
        {
            AnimateFullContainerHeight(FullTitlePreviewContainer.Height, 0, 200, Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut, () =>
            {
                _fullPreviewVisible = false;
            });
        };
        _fullPreviewOpacitySb.Begin();
    }

    private DispatcherTimer? _fullHeightAnimTimer;
    private void AnimateFullContainerHeight(double from, double to, int durationMs, Microsoft.UI.Xaml.Media.Animation.EasingMode easing, Action? onComplete)
    {
        _fullHeightAnimTimer?.Stop();
        if (Math.Abs(from - to) < 0.5)
        {
            FullTitlePreviewContainer.Height = to;
            onComplete?.Invoke();
            return;
        }

        FullTitlePreviewContainer.Height = from;
        double elapsed = 0;
        double interval = 1000.0 / 60.0;
        var ease = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = easing };

        _fullHeightAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _fullHeightAnimTimer.Tick += (s, e) =>
        {
            elapsed += interval;
            double progress = Math.Min(1.0, elapsed / durationMs);
            double easedProgress = ease.Ease(progress);
            FullTitlePreviewContainer.Height = from + (to - from) * easedProgress;

            if (progress >= 1.0)
            {
                _fullHeightAnimTimer!.Stop();
                FullTitlePreviewContainer.Height = to;
                onComplete?.Invoke();
            }
        };
        _fullHeightAnimTimer.Start();
    }

    private void UpdateFullPreviewTitlePosition(int align, bool animate = true)
    {
        if (FullPreviewTitleHost == null || FullPreviewTitleText == null || FullPreviewTitleTransform == null) return;

        double hostWidth = FullPreviewTitleHost.ActualWidth > 0 ? FullPreviewTitleHost.ActualWidth : 172;
        double textWidth = FullPreviewTitleText.ActualWidth > 0 ? FullPreviewTitleText.ActualWidth : 48;

        double targetX = align switch
        {
            0 => 0,
            1 => Math.Max(0, (hostWidth - textWidth) / 2.0),
            2 => Math.Max(0, hostWidth - textWidth),
            _ => 0
        };

        if (animate && Math.Abs(FullPreviewTitleTransform.X - targetX) > 0.5)
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = targetX,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
            };
            var sb = new Storyboard();
            Storyboard.SetTarget(anim, FullPreviewTitleTransform);
            Storyboard.SetTargetProperty(anim, "X");
            sb.Children.Add(anim);
            sb.Begin();
        }
        else
        {
            FullPreviewTitleTransform.X = targetX;
        }
    }

    private void FullTitleAlignmentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingFullUI || _currentSettingsGroup == null) return;
        int align = (int)Math.Round(e.NewValue);
        _currentSettingsGroup.TitleAlignment = align;
        GroupService.Instance.Save();

        ShowFullPreview();
        _fullPreviewHideTimer?.Stop();
        _fullPreviewHideTimer?.Start();

        UpdateFullPreviewTitlePosition(align);
    }
}
