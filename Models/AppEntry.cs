using System.ComponentModel;

namespace TaskTile.Models;

public class AppEntry : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    /// <summary>
    /// Path to the extracted .png icon cached on disk.
    /// May be empty if icon extraction failed.
    /// </summary>
    public string IconPath { get; set; } = string.Empty;
    public int TileSize { get; set; } = 1;
    public string Arguments { get; set; } = string.Empty;
    public bool RunAsAdmin { get; set; } = false;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Microsoft.UI.Xaml.Visibility IsIconVisible =>
        TaskTile.Services.SettingsService.Current.ShowPickerIcons && !string.IsNullOrEmpty(IconPath)
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility IsPathVisible =>
        TaskTile.Services.SettingsService.Current.ShowPickerFilePaths && !string.IsNullOrEmpty(ExePath)
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
}
