using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskTile.Models;
using TaskTile.Services;
using Windows.Graphics;
using WinRT.Interop;

namespace TaskTile.Popups;

public sealed class InstalledAppsPickerWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    private readonly Action<AppEntry> _onAppSelected;
    private readonly Action _onBrowseFile;
    private readonly ObservableCollection<AppEntry> _allApps = new();
    private readonly ObservableCollection<AppEntry> _displayApps = new();

    private TextBox _searchBox = null!;
    private ListView _listView = null!;
    private ProgressRing _loadingRing = null!;
    private TextBlock _statusText = null!;

    public InstalledAppsPickerWindow(RectInt32 parentRect, Action<AppEntry> onAppSelected, Action onBrowseFile)
    {
        _onAppSelected = onAppSelected;
        _onBrowseFile = onBrowseFile;

        try
        {
            ExtendsContentIntoTitleBar = true;
            if (AppWindow.TitleBar != null) AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        }
        catch { }

        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.IsResizable = false;
            op.IsMaximizable = false;
            op.IsMinimizable = false;
            op.IsAlwaysOnTop = true;
            op.SetBorderAndTitleBar(true, false);
        }
        try { AppWindow.IsShownInSwitchers = false; } catch { }

        var hwnd = WindowNative.GetWindowHandle(this);
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        try
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
        }
        catch { }

        BuildUI();
        PositionWindow(parentRect);
        _ = LoadAppsAsync();
    }

    private void PositionWindow(RectInt32 parentRect)
    {
        int w = 340;
        int h = 480;

        // Position floating adjacent to parent popup window
        int targetX;
        if (parentRect.X - w - 12 >= 20)
        {
            targetX = parentRect.X - w - 12; // place to the left
        }
        else
        {
            targetX = parentRect.X + parentRect.Width + 12; // place to the right
        }

        int targetY = parentRect.Y + parentRect.Height - h;
        if (targetY < 30) targetY = 30;

        AppWindow.MoveAndResize(new RectInt32(targetX, targetY, w, h));
    }

    private void BuildUI()
    {
        var root = new Grid
        {
            Padding = new Thickness(16, 12, 16, 14),
            RowSpacing = 10
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Apps List
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer / Browse file

        // Row 0: Header
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new FontIcon { Glyph = "\uE71D", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        titleStack.Children.Add(new TextBlock { Text = "Add Installed App", FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(titleStack, 0);
        header.Children.Add(titleStack);

        var closeBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 11 },
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0)
        };
        ToolTipService.SetToolTip(closeBtn, "Close");
        closeBtn.Click += (_, _) => this.Close();
        Grid.SetColumn(closeBtn, 1);
        header.Children.Add(closeBtn);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Row 1: Search box
        _searchBox = new TextBox
        {
            PlaceholderText = "Search installed apps...",
            CornerRadius = new CornerRadius(6)
        };
        _searchBox.TextChanged += (s, e) => ApplyFilter(_searchBox.Text);
        Grid.SetRow(_searchBox, 1);
        root.Children.Add(_searchBox);

        // Row 2: List & Loading
        var listContainer = new Grid();
        _loadingRing = new ProgressRing
        {
            IsActive = true,
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusText = new TextBlock
        {
            Text = "Loading apps...",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 48, 0, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        _listView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            ItemsSource = _displayApps
        };

        string itemTemplateXaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
    <Grid Padding=""4,4"" HorizontalAlignment=""Stretch"">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width=""Auto""/>
            <ColumnDefinition Width=""*""/>
        </Grid.ColumnDefinitions>
        <Image Grid.Column=""0"" Width=""24"" Height=""24"" Stretch=""Uniform"" Margin=""0,0,10,0"" Source=""{Binding IconPath}""/>
        <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"" Spacing=""1"">
            <TextBlock Text=""{Binding Name}"" FontSize=""12.5"" FontWeight=""SemiBold"" TextTrimming=""CharacterEllipsis""/>
            <TextBlock Text=""{Binding ExePath}"" FontSize=""10"" Foreground=""{ThemeResource TextFillColorSecondaryBrush}"" TextTrimming=""CharacterEllipsis""/>
        </StackPanel>
    </Grid>
</DataTemplate>";
        _listView.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(itemTemplateXaml);
        _listView.ItemClick += (s, e) =>
        {
            if (e.ClickedItem is AppEntry entry)
            {
                _onAppSelected(entry);
                this.Close();
            }
        };

        listContainer.Children.Add(_listView);
        listContainer.Children.Add(_loadingRing);
        listContainer.Children.Add(_statusText);

        Grid.SetRow(listContainer, 2);
        root.Children.Add(listContainer);

        // Row 3: Footer button for "Browse for specific file..."
        var browseBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE8E5", FontSize = 12 },
                    new TextBlock { Text = "Specific file instead...", FontSize = 12 }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6)
        };
        browseBtn.Click += (_, _) =>
        {
            this.Close();
            _onBrowseFile();
        };

        Grid.SetRow(browseBtn, 3);
        root.Children.Add(browseBtn);

        TaskTile.Services.SettingsService.Load();
        if (TaskTile.Services.SettingsService.Current.YpxMixUI)
        {
            var border = new Border
            {
                BorderBrush = TaskTile.Helpers.GlowUITheme.CreateGlowBorderBrush(false),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(14),
                Child = root
            };
            this.Content = border;
        }
        else
        {
            this.Content = root;
        }
    }

    private async Task LoadAppsAsync()
    {
        var discovered = await Task.Run(() => AppDiscoveryService.GetInstalledApps());

        // Fill icons on background
        _ = Task.Run(() =>
        {
            foreach (var app in discovered)
            {
                if (string.IsNullOrEmpty(app.IconPath) && !string.IsNullOrEmpty(app.ExePath))
                {
                    try { app.IconPath = IconHelper.GetOrExtractIcon(app.ExePath); } catch { }
                }
            }
        });

        DispatcherQueue.TryEnqueue(() =>
        {
            _allApps.Clear();
            foreach (var a in discovered) _allApps.Add(a);

            _loadingRing.IsActive = false;
            _loadingRing.Visibility = Visibility.Collapsed;
            _statusText.Visibility = Visibility.Collapsed;

            ApplyFilter(_searchBox?.Text ?? "");
        });
    }

    private void ApplyFilter(string query)
    {
        _displayApps.Clear();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var app in filtered)
        {
            _displayApps.Add(app);
        }
    }
}
