using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskTile.Models;
using TaskTile.Services;
using TaskTile.Popups;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TaskTile.Controls;

public sealed partial class GroupCard : UserControl
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int X;
        public int Y;
    }

    private AppGroup? _group;
    private bool _isUpdatingUI = false;

    public static event Action<GroupCard, AppGroup>? OpenFullSettingsRequested;
    public static event Action? EditModeRequested;

    private static readonly List<GroupCard> _activeCards = new();
    private static bool _isGlobalEditMode = false;
    private bool _isEditMode = false;
    private DispatcherTimer? _holdTimer;
    private Windows.Foundation.Point _pressPoint;

    public static void SetGlobalEditMode(bool enable)
    {
        _isGlobalEditMode = enable;
        foreach (var card in _activeCards.ToList())
        {
            card.SetEditMode(enable);
        }
    }

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
        this.Loaded += (s, e) =>
        {
            if (!_activeCards.Contains(this)) _activeCards.Add(this);
            if (_isGlobalEditMode)
            {
                this.DispatcherQueue?.TryEnqueue(() => SetEditMode(true));
            }
            if (_group != null)
            {
                int style = SettingsService.Current.GroupCardStyle;
                DefaultLayoutRoot.Visibility = (style == 0) ? Visibility.Visible : Visibility.Collapsed;
                VisitorIdLayoutRoot.Visibility = (style == 1) ? Visibility.Visible : Visibility.Collapsed;
                SuperCompactLayoutRoot.Visibility = (style == 2) ? Visibility.Visible : Visibility.Collapsed;

                if (style == 2) CardBorder.Padding = new Thickness(14, 8, 14, 8);
                else if (style == 1) CardBorder.Padding = new Thickness(14, 12, 14, 12);
                else CardBorder.Padding = new Thickness(16);
            }
            TaskbarService.GroupPinChanged -= OnGroupPinChanged;
            TaskbarService.GroupPinChanged += OnGroupPinChanged;
            UpdateSquarePlate(CardBorder.ActualWidth);
        };
        this.Unloaded += (s, e) =>
        {
            TaskbarService.GroupPinChanged -= OnGroupPinChanged;
            _activeCards.Remove(this);
            _holdTimer?.Stop();
        };
        CardBorder.SizeChanged += CardBorder_SizeChanged;
    }

    private void OnGroupPinChanged(AppGroup group)
    {
        if (_group?.Id == group.Id)
        {
            this.DispatcherQueue?.TryEnqueue(() =>
            {
                UpdatePinState(group.IsPinned);
            });
        }
    }

    private void UpdateSquarePlate(double cardWidth)
    {
        if (VisitorIconPlate != null && cardWidth > 40)
        {
            double padding = CardBorder.Padding.Left + CardBorder.Padding.Right;
            double avail = Math.Max(cardWidth - padding, 40);
            double size = Math.Min(avail, 114);
            VisitorIconPlate.Width = size;
            VisitorIconPlate.Height = size;
        }
    }

    private void CardBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            UpdateSquarePlate(e.NewSize.Width);
            try
            {
                var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(CardBorder);
                visual.CenterPoint = new System.Numerics.Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0f);
                if (_isEditMode)
                {
                    ApplyShakeAnimation(visual);
                }
            }
            catch { }
        }
    }

    private static void OnGroupDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GroupCard card && e.NewValue is AppGroup group)
        {
            card.Populate(group);
            if (_isGlobalEditMode)
            {
                card.DispatcherQueue?.TryEnqueue(() => card.SetEditMode(true));
            }
        }
    }

    public void Populate(AppGroup group)
    {
        _group = group;
        _isUpdatingUI = true;

        int style = SettingsService.Current.GroupCardStyle;
        DefaultLayoutRoot.Visibility = (style == 0) ? Visibility.Visible : Visibility.Collapsed;
        VisitorIdLayoutRoot.Visibility = (style == 1) ? Visibility.Visible : Visibility.Collapsed;
        SuperCompactLayoutRoot.Visibility = (style == 2) ? Visibility.Visible : Visibility.Collapsed;

        if (style == 2) CardBorder.Padding = new Thickness(14, 8, 14, 8);
        else if (style == 1) CardBorder.Padding = new Thickness(14, 12, 14, 12);
        else CardBorder.Padding = new Thickness(16);

        var name = string.IsNullOrEmpty(group.Name) ? "Group" : group.Name;
        GroupNameText.Text = name;
        VisitorGroupNameText.Text = name;
        SuperCompactGroupNameText.Text = name;

        bool useNiches = SettingsService.Current.UseNichesTerminology;
        RenameMenuItem.Text = useNiches ? "Rename Niche" : "Rename Group";
        DeleteMenuItem.Text = useNiches ? "Delete Niche" : "Delete Group";
        ToolTipService.SetToolTip(SettingsButton, useNiches ? "Niche settings" : "Group settings");
        ToolTipService.SetToolTip(VisitorSettingsButton, useNiches ? "Niche settings" : "Group settings");
        ToolTipService.SetToolTip(SuperCompactSettingsButton, useNiches ? "Niche settings" : "Group settings");
        ToolTipService.SetToolTip(DeleteButton, useNiches ? "Delete niche" : "Delete group");
        ToolTipService.SetToolTip(VisitorDeleteButton, useNiches ? "Delete niche" : "Delete group");
        ToolTipService.SetToolTip(SuperCompactDeleteButton, useNiches ? "Delete niche" : "Delete group");
        
        string countText;
        string glyph;
        if (group.GroupType == GroupType.Files)
        {
            countText = group.IsDynamicFolder ? "Dynamic folder" : (group.Apps.Count == 1 ? "1 file" : $"{group.Apps.Count} files");
            glyph = group.IsDynamicFolder ? "\uE8B7" : "\uE8A5";
        }
        else if (group.GroupType == GroupType.Radial)
        {
            countText = group.Apps.Count == 1 ? "1 app" : $"{group.Apps.Count} apps";
            glyph = "\uEE94";
        }
        else
        {
            countText = group.Apps.Count == 1 ? "1 app" : $"{group.Apps.Count} apps";
            glyph = "\uF168";
        }
        AppCountText.Text = countText;
        VisitorAppCountText.Text = countText;
        SuperCompactAppCountText.Text = countText;
        PlaceholderIcon.Glyph = glyph;
        VisitorPlaceholderIcon.Glyph = glyph;
        SuperCompactPlaceholderIcon.Glyph = glyph;

        bool isMono = group.MonochromeFolderIcon;

        if (!string.IsNullOrEmpty(group.CustomIconPath) && System.IO.File.Exists(group.CustomIconPath))
        {
            try
            {
                string iconPath = isMono
                    ? IconHelper.GetOrExtractMonochromeIcon(group.CustomIconPath)
                    : group.CustomIconPath;
                var bmp = new BitmapImage(new Uri(iconPath));
                GroupIconImage.Source = bmp;
                VisitorSingleImage.Source = bmp;
                SuperCompactSingleImage.Source = bmp;
            } catch {}
            GroupIconImage.Visibility = Visibility.Visible;
            VisitorSingleImage.Visibility = Visibility.Visible;
            SuperCompactSingleImage.Visibility = Visibility.Visible;
            VisitorGridIcons.Visibility = Visibility.Collapsed;
            SuperCompactGridIcons.Visibility = Visibility.Collapsed;
            PlaceholderIcon.Visibility = Visibility.Collapsed;
            VisitorPlaceholderIcon.Visibility = Visibility.Collapsed;
            SuperCompactPlaceholderIcon.Visibility = Visibility.Collapsed;
        }
        else if (group.Apps.Count > 0 || (group.IsDynamicFolder && !string.IsNullOrEmpty(group.DynamicFolderPath) && System.IO.Directory.Exists(group.DynamicFolderPath)))
        {
            var dispatcher = this.DispatcherQueue;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var paths = new List<string>();
                if (group.IsDynamicFolder && !string.IsNullOrEmpty(group.DynamicFolderPath) && System.IO.Directory.Exists(group.DynamicFolderPath))
                {
                    paths.AddRange(System.IO.Directory.GetFileSystemEntries(group.DynamicFolderPath)
                        .Where(f => (new System.IO.FileInfo(f).Attributes & (System.IO.FileAttributes.Hidden | System.IO.FileAttributes.System)) == 0)
                        .Take(4));
                }
                else
                {
                    paths.AddRange(group.Apps.Take(4).Select(a => a.ExePath));
                }

                var extractedPaths = new List<string>();
                foreach (var p in paths)
                {
                    string ep = isMono
                        ? IconHelper.GetOrExtractMonochromeIcon(p)
                        : IconHelper.GetOrExtractIcon(p);
                    if (!string.IsNullOrEmpty(ep) && System.IO.File.Exists(ep))
                    {
                        extractedPaths.Add(ep);
                    }
                }

                var iconPath = TaskbarService.GenerateGroupIcon(group, out string? pngPath);
                string? previewPath = !string.IsNullOrEmpty(pngPath) && System.IO.File.Exists(pngPath) ? pngPath : iconPath;

                dispatcher?.TryEnqueue(() =>
                {
                    try
                    {
                        PlaceholderIcon.Visibility = Visibility.Collapsed;
                        VisitorPlaceholderIcon.Visibility = Visibility.Collapsed;
                        SuperCompactPlaceholderIcon.Visibility = Visibility.Collapsed;

                        // Default layout icon
                        if (extractedPaths.Count == 1)
                        {
                            GroupIconImage.Source = new BitmapImage(new Uri(extractedPaths[0]));
                            GroupIconImage.Visibility = Visibility.Visible;
                        }
                        else if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath))
                        {
                            GroupIconImage.Source = new BitmapImage(new Uri(previewPath));
                            GroupIconImage.Visibility = Visibility.Visible;
                        }

                        // Visitor ID layout: clean native high-res icons (no black bg or borders)
                        if (extractedPaths.Count == 1)
                        {
                            VisitorSingleImage.Source = new BitmapImage(new Uri(extractedPaths[0]));
                            VisitorSingleImage.Visibility = Visibility.Visible;
                            VisitorGridIcons.Visibility = Visibility.Collapsed;
                        }
                        else if (extractedPaths.Count > 1)
                        {
                            VisitorSingleImage.Visibility = Visibility.Collapsed;
                            VisitorGridIcons.Visibility = Visibility.Visible;

                            Image[] slots = { VisitorIcon0, VisitorIcon1, VisitorIcon2, VisitorIcon3 };
                            for (int i = 0; i < slots.Length; i++)
                            {
                                if (i < extractedPaths.Count)
                                {
                                    slots[i].Source = new BitmapImage(new Uri(extractedPaths[i]));
                                    slots[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    slots[i].Source = null;
                                    slots[i].Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                        else
                        {
                            VisitorSingleImage.Visibility = Visibility.Collapsed;
                            VisitorGridIcons.Visibility = Visibility.Collapsed;
                            VisitorPlaceholderIcon.Visibility = Visibility.Visible;
                        }

                        // Super Compact layout:
                        if (extractedPaths.Count == 1)
                        {
                            SuperCompactSingleImage.Source = new BitmapImage(new Uri(extractedPaths[0]));
                            SuperCompactSingleImage.Visibility = Visibility.Visible;
                            SuperCompactGridIcons.Visibility = Visibility.Collapsed;
                        }
                        else if (extractedPaths.Count > 1)
                        {
                            SuperCompactSingleImage.Visibility = Visibility.Collapsed;
                            SuperCompactGridIcons.Visibility = Visibility.Visible;

                            Image[] scSlots = { SuperCompactIcon0, SuperCompactIcon1, SuperCompactIcon2, SuperCompactIcon3 };
                            for (int i = 0; i < scSlots.Length; i++)
                            {
                                if (i < extractedPaths.Count)
                                {
                                    scSlots[i].Source = new BitmapImage(new Uri(extractedPaths[i]));
                                    scSlots[i].Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    scSlots[i].Source = null;
                                    scSlots[i].Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                        else
                        {
                            SuperCompactSingleImage.Visibility = Visibility.Collapsed;
                            SuperCompactGridIcons.Visibility = Visibility.Collapsed;
                            SuperCompactPlaceholderIcon.Visibility = Visibility.Visible;
                        }
                    }
                    catch { }
                });
            });
        }
        else
        {
            GroupIconImage.Source = null;
            VisitorSingleImage.Source = null;
            SuperCompactSingleImage.Source = null;
            GroupIconImage.Visibility = Visibility.Collapsed;
            VisitorSingleImage.Visibility = Visibility.Collapsed;
            SuperCompactSingleImage.Visibility = Visibility.Collapsed;
            VisitorGridIcons.Visibility = Visibility.Collapsed;
            SuperCompactGridIcons.Visibility = Visibility.Collapsed;
            PlaceholderIcon.Visibility = Visibility.Visible;
            VisitorPlaceholderIcon.Visibility = Visibility.Visible;
            SuperCompactPlaceholderIcon.Visibility = Visibility.Visible;
        }

        UpdatePinState(group.IsPinned);

        // Populate Mini Quick Settings Flyout controls
        QuickSettingsTitleText.Text = SettingsService.Current.UseNichesTerminology ? "Niche Settings" : "Group Settings";

        if (group.GroupType == GroupType.Radial)
        {
            string appLabel = group.Apps.Count == 1 ? "1 app" : $"{group.Apps.Count} apps";
            AppCountText.Text = VisitorAppCountText.Text = SuperCompactAppCountText.Text = $"{appLabel} • Shortcut: {group.Shortcut}";
            StandardQuickSettingsPanel.Visibility = Visibility.Collapsed;
            RadialQuickSettingsPanel.Visibility = Visibility.Visible;
            RadialShortcutBox.Text = group.Shortcut ?? "Ctrl+Shift+A";
        }
        else
        {
            StandardQuickSettingsPanel.Visibility = Visibility.Visible;
            RadialQuickSettingsPanel.Visibility = Visibility.Collapsed;

            PopupStyleCombo.SelectedIndex = Math.Clamp(group.PopupStyle, 0, 4);
            PopupBackdropCombo.SelectedIndex = Math.Clamp(group.BackdropStyle, 0, 3);
            PopupThemeCombo.SelectedIndex = Math.Clamp(group.ThemeOverride, 0, 2);
            HideNameToggle.IsOn = group.HideName;
            HideAppLabelsToggle.IsOn = group.HideAppLabels;
            MiniTaskbarGapSlider.Value = group.TaskbarOffset;
            int titleAlign = group.TitleAlignment < 0 ? SettingsService.Current.TitleAlignment : group.TitleAlignment;
            MiniTitleAlignmentSlider.Value = Math.Clamp(titleAlign, 0, 2);
        }

        _isUpdatingUI = false;
    }

    private void UpdatePinState(bool isPinned)
    {
        if (_group?.GroupType == GroupType.Radial)
        {
            PinButton.Visibility = VisitorPinButton.Visibility = SuperCompactPinButton.Visibility = Visibility.Collapsed;
            SettingsButton.Visibility = VisitorSettingsButton.Visibility = SuperCompactSettingsButton.Visibility = Visibility.Visible;
            DeleteButton.Visibility = VisitorDeleteButton.Visibility = SuperCompactDeleteButton.Visibility = Visibility.Visible;
            return;
        }

        PinButton.Visibility = VisitorPinButton.Visibility = SuperCompactPinButton.Visibility = Visibility.Visible;
        var accentStyle = (Style)Application.Current.Resources["AccentButtonStyle"];
        if (isPinned)
        {
            PinIcon.Glyph = VisitorPinIcon.Glyph = SuperCompactPinIcon.Glyph = "\uE77A"; // Unpin
            PinText.Text = VisitorPinText.Text = SuperCompactPinText.Text = "Unpin";
            PinButton.Style = VisitorPinButton.Style = SuperCompactPinButton.Style = accentStyle;
            SettingsButton.Visibility = VisitorSettingsButton.Visibility = SuperCompactSettingsButton.Visibility = Visibility.Visible;
            DeleteButton.Visibility = VisitorDeleteButton.Visibility = SuperCompactDeleteButton.Visibility = Visibility.Visible;
        }
        else
        {
            PinIcon.Glyph = VisitorPinIcon.Glyph = SuperCompactPinIcon.Glyph = "\uE718"; // Pin
            PinText.Text = VisitorPinText.Text = SuperCompactPinText.Text = "Pin";
            PinButton.Style = VisitorPinButton.Style = SuperCompactPinButton.Style = null; // default style
            SettingsButton.Visibility = VisitorSettingsButton.Visibility = SuperCompactSettingsButton.Visibility = Visibility.Visible;
            DeleteButton.Visibility = VisitorDeleteButton.Visibility = SuperCompactDeleteButton.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(PinButton, 1);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        if (_group.IsPinned)
            TaskbarService.UnpinGroup(_group);
        else
            TaskbarService.PinGroup(_group);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        
        bool useNiches = SettingsService.Current.UseNichesTerminology;
        string entity = useNiches ? "Niche" : "Group";
        var dialog = new ContentDialog
        {
            Title = $"Delete {entity}?",
            Content = $"Are you sure you want to delete '{_group.Name}'?\n\nIf you have pinned this {entity.ToLower()} to the Taskbar, you must unpin it manually by right-clicking it.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (_group.IsPinned) TaskbarService.UnpinGroup(_group);
            GroupService.Instance.RemoveGroup(_group.Id);
            HotkeyService.Instance.UpdateAllHotkeys();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        if (sender is FrameworkElement fe)
            SettingsFlyout.ShowAt(fe);
        else
            SettingsFlyout.ShowAt(SettingsButton);
    }

    private void CloseMiniSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        SettingsFlyout.Hide();
    }

    private void FullscreenSettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        SettingsFlyout.Hide();
        OpenFullSettingsRequested?.Invoke(this, _group);
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || _group == null) return;

        if (_group.GroupType != GroupType.Radial)
        {
            if (PopupStyleCombo.SelectedIndex >= 0)
                _group.PopupStyle = PopupStyleCombo.SelectedIndex;
            if (PopupBackdropCombo.SelectedIndex >= 0)
                _group.BackdropStyle = PopupBackdropCombo.SelectedIndex;
            if (PopupThemeCombo.SelectedIndex >= 0)
                _group.ThemeOverride = PopupThemeCombo.SelectedIndex;

            _group.HideName = HideNameToggle.IsOn;
            _group.HideAppLabels = HideAppLabelsToggle.IsOn;
        }

        GroupService.Instance.Save();

        Populate(_group);
    }

    private void MiniTaskbarGapSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUI || _group == null) return;
        int val = (int)Math.Round(e.NewValue);
        _group.TaskbarOffset = val;
        GroupService.Instance.Save();
    }

    private void MiniTitleAlignmentSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingUI || _group == null) return;
        int val = (int)Math.Round(e.NewValue);
        _group.TitleAlignment = val;
        GroupService.Instance.Save();
    }

    private void RadialShortcutBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI || _group == null) return;
        _group.Shortcut = RadialShortcutBox.Text.Trim();
        GroupService.Instance.Save();
        HotkeyService.Instance.UpdateAllHotkeys();
        AppCountText.Text = $"{_group.Apps.Count} apps • Shortcut: {_group.Shortcut}";
    }

    private void TestRadialBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_group == null) return;
        SettingsFlyout.Hide();
        RadialMenuManager.Show(_group);
    }

    private static GroupCard? _activeRenamingCard;

    public static void CommitActiveRename()
    {
        if (_activeRenamingCard != null)
        {
            var card = _activeRenamingCard;
            _activeRenamingCard = null;
            card.CommitAnyInlineRename();
        }
    }

    public void CommitAnyInlineRename()
    {
        if (GroupNameEditBox.Visibility == Visibility.Visible)
            CommitInlineRename();
        if (VisitorGroupNameEditBox.Visibility == Visibility.Visible)
            CommitVisitorInlineRename();
        if (SuperCompactGroupNameEditBox.Visibility == Visibility.Visible)
            CommitSuperCompactInlineRename();
    }

    private void EditBox_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        _holdTimer?.Stop();
    }

    private void GroupNameText_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        _holdTimer?.Stop();
        BeginInlineRename();
    }

    private void RenameContext_Click(object sender, RoutedEventArgs e)
    {
        int style = SettingsService.Current.GroupCardStyle;
        if (style == 2)
            BeginSuperCompactInlineRename();
        else if (style == 1)
            BeginVisitorInlineRename();
        else
            BeginInlineRename();
    }

    private void BeginInlineRename()
    {
        if (_group == null) return;
        CommitActiveRename();
        _activeRenamingCard = this;
        _holdTimer?.Stop();
        GroupNameText.Visibility = Visibility.Collapsed;
        GroupNameEditBox.Visibility = Visibility.Visible;
        GroupNameEditBox.Text = _group.Name;
        GroupNameEditBox.SelectAll();
        GroupNameEditBox.Focus(FocusState.Programmatic);
    }

    private void CommitInlineRename()
    {
        if (_group == null || GroupNameEditBox.Visibility != Visibility.Visible) return;
        var newName = GroupNameEditBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _group.Name = newName;
            GroupNameText.Text = VisitorGroupNameText.Text = SuperCompactGroupNameText.Text = string.IsNullOrEmpty(_group.Name) ? "Group" : _group.Name;
            GroupService.Instance.Save();
            if (_group.IsPinned) TaskbarService.UpdateGroupShortcut(_group);
        }
        GroupNameEditBox.Visibility = Visibility.Collapsed;
        GroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void CancelInlineRename()
    {
        GroupNameEditBox.Visibility = Visibility.Collapsed;
        GroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void GroupNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
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

    private void GroupNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitInlineRename();
    }

    private void VisitorGroupNameText_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        _holdTimer?.Stop();
        BeginVisitorInlineRename();
    }

    private void BeginVisitorInlineRename()
    {
        if (_group == null) return;
        CommitActiveRename();
        _activeRenamingCard = this;
        _holdTimer?.Stop();
        VisitorGroupNameText.Visibility = Visibility.Collapsed;
        VisitorGroupNameEditBox.Visibility = Visibility.Visible;
        VisitorGroupNameEditBox.Text = _group.Name;
        VisitorGroupNameEditBox.SelectAll();
        VisitorGroupNameEditBox.Focus(FocusState.Programmatic);
    }

    private void CommitVisitorInlineRename()
    {
        if (_group == null || VisitorGroupNameEditBox.Visibility != Visibility.Visible) return;
        var newName = VisitorGroupNameEditBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _group.Name = newName;
            GroupNameText.Text = VisitorGroupNameText.Text = SuperCompactGroupNameText.Text = string.IsNullOrEmpty(_group.Name) ? "Group" : _group.Name;
            GroupService.Instance.Save();
            if (_group.IsPinned) TaskbarService.UpdateGroupShortcut(_group);
        }
        VisitorGroupNameEditBox.Visibility = Visibility.Collapsed;
        VisitorGroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void CancelVisitorInlineRename()
    {
        VisitorGroupNameEditBox.Visibility = Visibility.Collapsed;
        VisitorGroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void VisitorGroupNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitVisitorInlineRename();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelVisitorInlineRename();
            e.Handled = true;
        }
    }

    private void VisitorGroupNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitVisitorInlineRename();
    }

    private void SuperCompactGroupNameText_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        _holdTimer?.Stop();
        BeginSuperCompactInlineRename();
    }

    private void BeginSuperCompactInlineRename()
    {
        if (_group == null) return;
        CommitActiveRename();
        _activeRenamingCard = this;
        _holdTimer?.Stop();
        SuperCompactGroupNameText.Visibility = Visibility.Collapsed;
        SuperCompactGroupNameEditBox.Visibility = Visibility.Visible;
        SuperCompactGroupNameEditBox.Text = _group.Name;
        SuperCompactGroupNameEditBox.SelectAll();
        SuperCompactGroupNameEditBox.Focus(FocusState.Programmatic);
    }

    private void CommitSuperCompactInlineRename()
    {
        if (_group == null || SuperCompactGroupNameEditBox.Visibility != Visibility.Visible) return;
        var newName = SuperCompactGroupNameEditBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            _group.Name = newName;
            GroupNameText.Text = VisitorGroupNameText.Text = SuperCompactGroupNameText.Text = string.IsNullOrEmpty(_group.Name) ? "Group" : _group.Name;
            GroupService.Instance.Save();
            if (_group.IsPinned) TaskbarService.UpdateGroupShortcut(_group);
        }
        SuperCompactGroupNameEditBox.Visibility = Visibility.Collapsed;
        SuperCompactGroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void CancelSuperCompactInlineRename()
    {
        SuperCompactGroupNameEditBox.Visibility = Visibility.Collapsed;
        SuperCompactGroupNameText.Visibility = Visibility.Visible;
        if (_activeRenamingCard == this) _activeRenamingCard = null;
    }

    private void SuperCompactGroupNameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitSuperCompactInlineRename();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelSuperCompactInlineRename();
            e.Handled = true;
        }
    }

    private void SuperCompactGroupNameEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitSuperCompactInlineRename();
    }

    private void VisitorIconPlate_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && Math.Abs(VisitorIconPlate.Height - e.NewSize.Width) > 1)
        {
            VisitorIconPlate.Height = e.NewSize.Width;
        }
    }

    private void ApplyShakeAnimation(Microsoft.UI.Composition.Visual visual)
    {
        var comp = visual.Compositor;
        float w = (float)CardBorder.ActualWidth;
        float h = (float)CardBorder.ActualHeight;
        if (w > 0 && h > 0)
        {
            visual.CenterPoint = new System.Numerics.Vector3(w / 2f, h / 2f, 0f);
        }

        var ease = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.42f, 0f), new System.Numerics.Vector2(0.58f, 1f));
        var shakeAnim = comp.CreateScalarKeyFrameAnimation();
        shakeAnim.InsertKeyFrame(0.00f, 0f);
        shakeAnim.InsertKeyFrame(0.25f, -1.25f, ease);
        shakeAnim.InsertKeyFrame(0.50f, 0f, ease);
        shakeAnim.InsertKeyFrame(0.75f, 1.25f, ease);
        shakeAnim.InsertKeyFrame(1.00f, 0f, ease);
        shakeAnim.Duration = TimeSpan.FromMilliseconds(220 + (Math.Abs(this.GetHashCode()) % 40));
        shakeAnim.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
        visual.StartAnimation("RotationAngleInDegrees", shakeAnim);
    }

    public void PlayRadialSquishSquash(UIElement? element)
    {
        if (element == null) return;
        try
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
            var comp = visual.Compositor;
            float w = (float)element.ActualSize.X;
            float h = (float)element.ActualSize.Y;
            if (w <= 0) w = 32f;
            if (h <= 0) h = 32f;
            visual.CenterPoint = new System.Numerics.Vector3(w / 2f, h / 2f, 0f);

            var ease = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.3f, 0.0f), new System.Numerics.Vector2(0.2f, 1.0f));

            var squish = comp.CreateVector3KeyFrameAnimation();
            squish.InsertKeyFrame(0.00f, new System.Numerics.Vector3(1.00f, 1.00f, 1.0f));
            squish.InsertKeyFrame(0.25f, new System.Numerics.Vector3(1.22f, 0.78f, 1.0f), ease); // squish
            squish.InsertKeyFrame(0.55f, new System.Numerics.Vector3(0.85f, 1.18f, 1.0f), ease); // stretch
            squish.InsertKeyFrame(0.80f, new System.Numerics.Vector3(1.04f, 0.96f, 1.0f), ease); // rebound
            squish.InsertKeyFrame(1.00f, new System.Numerics.Vector3(1.00f, 1.00f, 1.0f), ease); // settle
            squish.Duration = TimeSpan.FromMilliseconds(380);
            visual.StartAnimation("Scale", squish);
        }
        catch { }
    }

    public void SetEditMode(bool enable)
    {
        _isEditMode = enable;
        CardBorder.CanDrag = enable;

        try
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(CardBorder);
            var comp = visual.Compositor;

            if (enable)
            {
                float w = (float)CardBorder.ActualWidth;
                float h = (float)CardBorder.ActualHeight;
                if (w > 0 && h > 0)
                {
                    visual.CenterPoint = new System.Numerics.Vector3(w / 2f, h / 2f, 0f);
                }

                // Stop any previous rotation before starting bounce
                visual.StopAnimation("RotationAngleInDegrees");
                visual.RotationAngleInDegrees = 0f;

                // 1. Bounce animation FIRST (shrink -> spring pop -> settle)
                var batch = comp.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

                var easeOut = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.1f, 0.9f), new System.Numerics.Vector2(0.2f, 1.0f));
                var easeIn = comp.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.4f, 0.0f), new System.Numerics.Vector2(0.6f, 1.0f));

                var scaleAnim = comp.CreateVector3KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(0.00f, new System.Numerics.Vector3(1.00f, 1.00f, 1f));
                scaleAnim.InsertKeyFrame(0.28f, new System.Numerics.Vector3(0.90f, 0.90f, 1f), easeIn); // shrink in anticipation
                scaleAnim.InsertKeyFrame(0.65f, new System.Numerics.Vector3(1.06f, 1.06f, 1f), easeOut); // spring pop
                scaleAnim.InsertKeyFrame(0.85f, new System.Numerics.Vector3(0.98f, 0.98f, 1f)); // slight rebound
                scaleAnim.InsertKeyFrame(1.00f, new System.Numerics.Vector3(1.00f, 1.00f, 1f)); // settle
                scaleAnim.Duration = TimeSpan.FromMilliseconds(330);
                visual.StartAnimation("Scale", scaleAnim);

                // 2. Only start shaking AFTER the bounce settles!
                batch.Completed += (s, ev) =>
                {
                    if (_isEditMode)
                    {
                        ApplyShakeAnimation(visual);
                    }
                };
                batch.End();
            }
            else
            {
                visual.StopAnimation("RotationAngleInDegrees");
                visual.RotationAngleInDegrees = 0f;

                var restoreAnim = comp.CreateVector3KeyFrameAnimation();
                restoreAnim.InsertKeyFrame(1.0f, new System.Numerics.Vector3(1f, 1f, 1f));
                restoreAnim.Duration = TimeSpan.FromMilliseconds(150);
                visual.StartAnimation("Scale", restoreAnim);
            }
        }
        catch { }
    }

    private void CardBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (_group?.GroupType == GroupType.Radial)
        {
            PlayRadialSquishSquash(VisitorPlaceholderIcon);
            PlayRadialSquishSquash(PlaceholderIcon);
            PlayRadialSquishSquash(SuperCompactPlaceholderIcon);
        }
    }

    private void CardBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_group?.GroupType == GroupType.Radial)
        {
            PlayRadialSquishSquash(VisitorPlaceholderIcon);
            PlayRadialSquishSquash(PlaceholderIcon);
            PlayRadialSquishSquash(SuperCompactPlaceholderIcon);
        }

        if (_activeRenamingCard != null)
        {
            CommitActiveRename();
            _holdTimer?.Stop();
            e.Handled = true;
            return;
        }

        if (_isEditMode) return;
        _pressPoint = e.GetCurrentPoint(CardBorder).Position;
        _holdTimer?.Stop();
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _holdTimer.Tick += (s, ev) =>
        {
            _holdTimer.Stop();
            EditModeRequested?.Invoke();
        };
        _holdTimer.Start();
    }

    private void CardBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _holdTimer?.Stop();
        if (_isEditMode)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(CardBorder);
                    ApplyShakeAnimation(visual);
                }
                catch { }
            });
        }
    }

    private void CardBorder_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _holdTimer?.Stop();
    }

    private void CardBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_holdTimer != null && _holdTimer.IsEnabled)
        {
            var cur = e.GetCurrentPoint(CardBorder).Position;
            if (Math.Abs(cur.X - _pressPoint.X) > 10 || Math.Abs(cur.Y - _pressPoint.Y) > 10)
            {
                _holdTimer.Stop();
            }
        }
    }

    private void CardBorder_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (_group == null || !_isEditMode)
        {
            args.Cancel = true;
            return;
        }
        args.Data.SetText(_group.Id.ToString());
        args.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void CardBorder_DragOver(object sender, DragEventArgs e)
    {
        if (_isEditMode)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }
    }

    private async void CardBorder_Drop(object sender, DragEventArgs e)
    {
        if (!_isEditMode || _group == null) return;
        try
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                if (Guid.TryParse(text, out var sourceId))
                {
                    var groups = GroupService.Instance.Groups;
                    var sourceGroup = groups.FirstOrDefault(g => g.Id == sourceId);
                    if (sourceGroup != null && sourceGroup != _group)
                    {
                        int oldIndex = groups.IndexOf(sourceGroup);
                        int newIndex = groups.IndexOf(_group);
                        if (oldIndex >= 0 && newIndex >= 0)
                        {
                            groups.Move(oldIndex, newIndex);
                            GroupService.Instance.Save();
                            if (_isGlobalEditMode)
                            {
                                this.DispatcherQueue?.TryEnqueue(() => SetGlobalEditMode(true));
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }
}