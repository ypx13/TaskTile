using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskTile.Models;
using TaskTile.Services;
using System.Linq;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;

namespace TaskTile.Controls;

public sealed partial class GroupCard : UserControl
{
    private AppGroup? _group;
    private bool _isUpdatingUI = false;

    public static readonly DependencyProperty GroupDataProperty =
        DependencyProperty.Register(nameof(GroupData), typeof(AppGroup), typeof(GroupCard),
            new PropertyMetadata(null, OnGroupDataChanged));

    public AppGroup? GroupData
    {
        get => (AppGroup?)GetValue(GroupDataProperty);
        set => SetValue(GroupDataProperty, value);
    }

    public GroupCard()
    {
        this.InitializeComponent();
    }

    private static void OnGroupDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GroupCard card && e.NewValue is AppGroup group)
        {
            card.Populate(group);
        }
    }

    private void Populate(AppGroup group)
    {
        _group = group;
        _isUpdatingUI = true;

        GroupNameText.Text = string.IsNullOrEmpty(group.Name) ? "Group" : group.Name;
        
        if (group.GroupType == GroupType.Files)
        {
            AppCountText.Text = group.IsDynamicFolder ? "Dynamic folder" : (group.Apps.Count == 1 ? "1 file" : $"{group.Apps.Count} files");
            PlaceholderIcon.Glyph = group.IsDynamicFolder ? "\uE8B7" : "\uE8A5";
        }
        else
        {
            AppCountText.Text = group.Apps.Count == 1 ? "1 app" : $"{group.Apps.Count} apps";
            PlaceholderIcon.Glyph = "\uF168";
        }

        if (!string.IsNullOrEmpty(group.CustomIconPath) && System.IO.File.Exists(group.CustomIconPath))
        {
            try { GroupIconImage.Source = new BitmapImage(new Uri(group.CustomIconPath)); } catch {}
            GroupIconImage.Visibility = Visibility.Visible;
            PlaceholderIcon.Visibility = Visibility.Collapsed;
            ClearCustomIconButton.Visibility = Visibility.Visible;
        }
        else if (group.Apps.Count > 0 || (group.IsDynamicFolder && !string.IsNullOrEmpty(group.DynamicFolderPath) && System.IO.Directory.Exists(group.DynamicFolderPath)))
        {
            ClearCustomIconButton.Visibility = Visibility.Collapsed;
            var dispatcher = this.DispatcherQueue;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var iconPath = TaskbarService.GenerateGroupIcon(group, out string? pngPath);
                string? previewPath = !string.IsNullOrEmpty(pngPath) && System.IO.File.Exists(pngPath) ? pngPath : iconPath;
                if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath))
                {
                    dispatcher?.TryEnqueue(() =>
                    {
                        try
                        {
                            GroupIconImage.Source = new BitmapImage(new Uri(previewPath));
                            GroupIconImage.Visibility = Visibility.Visible;
                            PlaceholderIcon.Visibility = Visibility.Collapsed;
                        }
                        catch { }
                    });
                }
            });
        }
        else
        {
            ClearCustomIconButton.Visibility = Visibility.Collapsed;
            GroupIconImage.Source = null;
            GroupIconImage.Visibility = Visibility.Collapsed;
            PlaceholderIcon.Visibility = Visibility.Visible;
        }

        UpdatePinState(group.IsPinned);

        // Load settings
        PopupStyleCombo.SelectedIndex = group.PopupStyle;
        HideNameToggle.IsOn = group.HideName;
        HideAppLabelsToggle.IsOn = group.HideAppLabels;
        MarqueeAppLabelsToggle.IsOn = group.MarqueeAppLabels;
        ScrollAppLabelsToggle.IsOn = group.ScrollAppLabels;
        ShowCardLabelsToggle.IsOn = group.ShowCardLabels;
        OverrideLaunchSideToggle.IsOn = group.OverrideLaunchSide;
        LaunchPositionCombo.SelectedIndex = group.GroupLaunchSide;
        OverrideBorderColorToggle.IsOn = group.OverrideBorderColor;
        try {
            string hex = group.CustomBorderColor;
            if (!string.IsNullOrEmpty(hex) && hex.Length >= 7) {
                byte r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
                CustomBorderColorPicker.Color = Windows.UI.Color.FromArgb(255, r, g, b);
            }
        } catch {}
        DisableAnimationToggle.IsOn = group.DisableAnimation;
        DisableFloatToggle.IsOn = group.DisableFloat;
        DisableRoundedCornersToggle.IsOn = group.DisableRoundedCorners;
        DisableAutoHideToggle.IsOn = group.DisableAutoHide;
        MakeMainFocusToggle.IsOn = group.MakeMainFocus;
        KeepOpenToggle.IsOn = group.KeepOpen;
        DesktopModeToggle.IsOn = group.IsDesktopMode;
        PopupBackdropCombo.SelectedIndex = group.BackdropStyle;
        FolderIconStyleCombo.SelectedIndex = group.AppIconStyle;
        AlignmentCombo.SelectedIndex = group.CompactAlignment;
        MonochromeFolderIconToggle.IsOn = group.MonochromeFolderIcon;
        AppIconStyleCombo.SelectedIndex = group.AppIconStyle;
        ThemeOverrideCombo.SelectedIndex = group.ThemeOverride;
        TitleAlignmentSlider.Value = group.TitleAlignment;
        ColumnsBox.Value = group.GridColumns;
        RowsBox.Value = group.GridRows;

        _isUpdatingUI = false;
        UpdateVisibility();
    }

    private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
        HoverIn.Begin();
    }

    private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
        HoverOut.Begin();
    }

    private void UpdatePinState(bool isPinned)
    {
        if (isPinned)
        {
            PinIcon.Glyph = "\uE77A"; // Unpin
            PinText.Text = "Remove Shortcut";
            PinButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            SettingsButton.Visibility = Visibility.Visible;
            DeleteButton.Visibility = Visibility.Visible;
        }
        else
        {
            PinIcon.Glyph = "\uE718"; // Pin
            PinText.Text = "Desktop Shortcut";
            PinButton.Style = null; // default style
            SettingsButton.Visibility = Visibility.Visible;
            DeleteButton.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(PinButton, 1);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        _group.IsPinned = !_group.IsPinned;
        if (_group.IsPinned)
            TaskbarService.PinGroup(_group);
        else
            TaskbarService.UnpinGroup(_group);
        UpdatePinState(_group.IsPinned);
        GroupService.Instance.Save();
        UpdateVisibility();
    }



    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        
        var dialog = new ContentDialog
        {
            Title = "Delete Group?",
            Content = $"Are you sure you want to delete '{_group.Name}'?\n\nIf you have pinned this group to the Taskbar, you must unpin it manually by right-clicking it.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (_group.IsPinned) TaskbarService.UnpinGroup(_group);
            GroupService.Instance.RemoveGroup(_group.Id);
        }
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || _group == null) return;

        _group.PopupStyle = PopupStyleCombo.SelectedIndex;
        _group.HideName = HideNameToggle.IsOn;
        _group.HideAppLabels = HideAppLabelsToggle.IsOn;
        _group.MarqueeAppLabels = MarqueeAppLabelsToggle.IsOn;
        _group.ScrollAppLabels = ScrollAppLabelsToggle.IsOn;
        _group.ShowCardLabels = ShowCardLabelsToggle.IsOn;
        _group.OverrideLaunchSide = OverrideLaunchSideToggle.IsOn;
        _group.OverrideBorderColor = OverrideBorderColorToggle.IsOn;
        _group.GroupLaunchSide = LaunchPositionCombo.SelectedIndex;
        _group.DisableAnimation = DisableAnimationToggle.IsOn;
        _group.DisableFloat = DisableFloatToggle.IsOn;
        _group.DisableRoundedCorners = DisableRoundedCornersToggle.IsOn;
        _group.DisableAutoHide = DisableAutoHideToggle.IsOn;
        _group.MakeMainFocus = MakeMainFocusToggle.IsOn;
        _group.KeepOpen = KeepOpenToggle.IsOn;
        _group.IsDesktopMode = DesktopModeToggle.IsOn;
        _group.BackdropStyle = PopupBackdropCombo.SelectedIndex;
        _group.FolderIconStyle = FolderIconStyleCombo.SelectedIndex;
        _group.CompactAlignment = AlignmentCombo.SelectedIndex;
        _group.MonochromeFolderIcon = MonochromeFolderIconToggle.IsOn;
        _group.AppIconStyle = AppIconStyleCombo.SelectedIndex;
        _group.ThemeOverride = ThemeOverrideCombo.SelectedIndex;
        _group.TitleAlignment = (int)TitleAlignmentSlider.Value;

        GroupService.Instance.Save();
        UpdateVisibility();

        if (_group.IsPinned)
            TaskbarService.PinGroup(_group); // update pin with new settings if needed
    }

    private void NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isUpdatingUI || _group == null) return;
        
        if (sender == ColumnsBox) _group.GridColumns = (int)Math.Max(0, Math.Min(10, args.NewValue));
        else if (sender == RowsBox) _group.GridRows = (int)Math.Max(0, Math.Min(10, args.NewValue));
        
        GroupService.Instance.Save();
        UpdateVisibility();
    }

    private async void CustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;

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
            _group.CustomIconPath = file.Path;
            ClearCustomIconButton.Visibility = Visibility.Visible;
            if (_group.IsPinned) TaskbarService.PinGroup(_group);
            Populate(_group);
            GroupService.Instance.Save();
        }
    }

    private void ClearCustomIconButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        _group.CustomIconPath = string.Empty;
        ClearCustomIconButton.Visibility = Visibility.Collapsed;
        if (_group.IsPinned) TaskbarService.PinGroup(_group);
        Populate(_group);
        GroupService.Instance.Save();
    }

    private void UpdateVisibility()
    {
        if (_group == null) return;
        bool isClassic = _group.PopupStyle == 0;
        bool isModern = _group.PopupStyle == 2;
        bool showOverrides = (isClassic || isModern);

        OverrideLaunchSideToggle.Visibility = showOverrides ? Visibility.Visible : Visibility.Collapsed;

        if (showOverrides && OverrideLaunchSideToggle.IsOn) {
            LaunchPositionCombo.Visibility = Visibility.Visible;
            MakeMainFocusToggle.Visibility = LaunchPositionCombo.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        } else {
            LaunchPositionCombo.Visibility = Visibility.Collapsed;
            MakeMainFocusToggle.Visibility = Visibility.Collapsed;
        }

        // Border color override is hidden for now
        BorderColorButton.Visibility = OverrideBorderColorToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CustomBorderColorPicker_ColorChanged(Microsoft.UI.Xaml.Controls.ColorPicker sender, Microsoft.UI.Xaml.Controls.ColorChangedEventArgs args)
    {
        if (_isUpdatingUI || _group == null) return;
        var c = args.NewColor;
        _group.CustomBorderColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        GroupService.Instance.Save();
    }
}