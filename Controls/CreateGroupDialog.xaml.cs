using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TaskTile.Models;
using TaskTile.Services;
using Windows.UI;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace TaskTile.Controls;

public sealed partial class CreateGroupDialog : ContentDialog
{
    public AppGroup? ResultGroup { get; private set; }

    private System.Collections.ObjectModel.ObservableCollection<AppEntry> _allApps = new();
    private System.Collections.ObjectModel.ObservableCollection<AppEntry> _displayApps = new();

    public CreateGroupDialog()
    {
        this.InitializeComponent();
        this.Opened += async (_, _) => await LoadAppsAsync();
    }

    private async Task LoadAppsAsync()
    {
        LoadingRing.IsActive = true;
        AppListView.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        var discovered = await Task.Run(() => AppDiscoveryService.GetInstalledApps());
        _allApps = new System.Collections.ObjectModel.ObservableCollection<AppEntry>(discovered);
        AppListView.ItemsSource = _displayApps;

        ApplyFilter(string.Empty);

        LoadingRing.IsActive = false;
        AppListView.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void ApplyFilter(string query)
    {
        var oldSelections = AppListView.SelectedItems.OfType<AppEntry>().ToList();

        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps.ToList()
            : _allApps.Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _displayApps.Clear();
        foreach (var item in filtered)
        {
            _displayApps.Add(item);
        }

        foreach (var sel in oldSelections)
        {
            if (_displayApps.Contains(sel))
                AppListView.SelectedItems.Add(sel);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
    }

    private void AppListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int count = AppListView.SelectedItems.Count;
        SelectedCountText.Text = count == 0 ? "0 selected" :
                                 count == 1 ? "1 selected" : $"{count} selected";
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        // Supported types: executables, installers, scripts, and shortcuts
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".msi");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".lnk");
        picker.FileTypeFilter.Add(".url");
        picker.FileTypeFilter.Add(".dll");

        // Initialize the picker with the current window handle (required for unpackaged WinUI 3)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var selectedFile = await picker.PickSingleFileAsync();
        if (selectedFile == null) return;

        // Auto-extract a friendly display name from file version info, fallback to filename
        string friendlyName = await Task.Run(() =>
        {
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(selectedFile.Path);
                if (!string.IsNullOrWhiteSpace(info.ProductName))       return info.ProductName.Trim();
                if (!string.IsNullOrWhiteSpace(info.FileDescription))   return info.FileDescription.Trim();
            }
            catch { }
            return System.IO.Path.GetFileNameWithoutExtension(selectedFile.Path);
        });

        var appEntry = new AppEntry
        {
            Name     = friendlyName,
            ExePath  = selectedFile.Path,
            IconPath = IconHelper.GetOrExtractIcon(selectedFile.Path),
        };

        _allApps.Insert(0, appEntry);
        SearchBox.Text = string.Empty; 
        ApplyFilter(string.Empty);
        
        // Slight delay to allow UI to generate the container
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!_displayApps.Contains(appEntry)) return;
            AppListView.SelectedItems.Add(appEntry);
            AppListView.ScrollIntoView(appEntry);
        });
    }


    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = GroupNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            GroupNameBox.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }
        
        var selectedApps = AppListView.SelectedItems.OfType<AppEntry>().ToList();
        if (selectedApps.Count == 0)
        {
            SelectedCountText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
            return;
        }

        LoadingRing.IsActive = true;
        AppListView.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;



        // Extract and assign heavy icon caches in background
        await Task.Run(() =>
        {
            foreach (var app in selectedApps)
            {
                app.IconPath = IconHelper.GetOrExtractIcon(app.ExePath);
            }
        });

        ResultGroup = new AppGroup
        {
            Name = name,
            Apps = selectedApps
        };

        this.Hide();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResultGroup = null;
        this.Hide();
    }
}
