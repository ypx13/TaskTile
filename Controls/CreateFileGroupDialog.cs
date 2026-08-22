using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using TaskTile.Models;
using TaskTile.Services;

namespace TaskTile.Controls;

public sealed class CreateFileGroupDialog : ContentDialog
{
    public AppGroup? ResultGroup { get; private set; }

    private readonly ObservableCollection<AppEntry> _files = new();
    private readonly ListView _listView;
    private readonly TextBox _nameBox;
    private readonly TextBlock _countText;
    private readonly CheckBox _dynamicFolderCheck;
    private readonly Button _addBtn;

    public CreateFileGroupDialog()
    {
        Title = "New File Group";
        PrimaryButtonText   = "Create";
        CloseButtonText     = "Cancel";
        DefaultButton       = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        _nameBox = new TextBox
        {
            PlaceholderText = "Group name…",
            Margin = new Thickness(0, 0, 0, 12)
        };
        _nameBox.TextChanged += (_, _) => UpdateCreateEnabled();

        _addBtn = new Button
        {
            Content  = "＋  Add Files…",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin   = new Thickness(0, 0, 0, 8)
        };
        _addBtn.Click += AddFiles_Click;

        var expTitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        expTitle.Children.Add(new TextBlock { Text = "Dynamic folder (auto-sync folder contents)", VerticalAlignment = VerticalAlignment.Center });
        expTitle.Children.Add(new Border { CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 180, 0)), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "Experimental", FontSize = 10, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 200, 50)) } });

        _dynamicFolderCheck = new CheckBox
        {
            Content = expTitle,
            Margin = new Thickness(0, 4, 0, 12)
        };
        _dynamicFolderCheck.Checked += (_, _) =>
        {
            _files.Clear();
            UpdateCountText();
            UpdateCreateEnabled();
            UpdateAddButtonUI();
            AddFiles_Click(this, new RoutedEventArgs());
        };
        _dynamicFolderCheck.Unchecked += (_, _) =>
        {
            _files.Clear();
            UpdateCountText();
            UpdateCreateEnabled();
            UpdateAddButtonUI();
        };

        _countText = new TextBlock
        {
            Text       = "0 files selected",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize   = 12,
            Margin     = new Thickness(0, 0, 0, 6)
        };

        _listView = new ListView
        {
            ItemsSource  = _files,
            MaxHeight    = 300,
            SelectionMode = ListViewSelectionMode.None
        };
        _listView.ItemTemplate = BuildItemTemplate();

        Content = new StackPanel
        {
            MinWidth = 320,
            MaxWidth = 450,
            Spacing = 0,
            Children =
            {
                _nameBox,
                _dynamicFolderCheck,
                _addBtn,
                _countText,
                _listView
            }
        };

        UpdateAddButtonUI();
        PrimaryButtonClick += OnCreate;
    }

    private void UpdateAddButtonUI()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        sp.Children.Add(new FontIcon { Glyph = (_dynamicFolderCheck.IsChecked == true) ? "\xE838" : "\xE710", FontSize = 14, Margin = new Thickness(0, 2, 0, 0) });
        sp.Children.Add(new TextBlock { Text = (_dynamicFolderCheck.IsChecked == true) ? "Choose Folder…" : "Add Files…" });
        _addBtn.Content = sp;
    }

    private DataTemplate BuildItemTemplate()
    {
        string dummyXaml = @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Grid/></DataTemplate>";
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(dummyXaml);
    }

    public CreateFileGroupDialog WithGeneratedTemplate()
    {
        _listView.ItemTemplate = null; // use default string representation
        // Override in-process: hook ContainerContentChanging
        _listView.ContainerContentChanging += (lv, args) =>
        {
            if (args.Item is not AppEntry entry) return;
            args.Handled = true;

            var icon = new FontIcon { Glyph = "\uE8A5", FontSize = 16, Margin = new Thickness(0,0,8,0) };
            var name = new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 };
            var path = new TextBlock { Text = entry.ExePath, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260 };
            var removeBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 13 },
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                Tag = entry
            };
            removeBtn.Click += (_, _) =>
            {
                _files.Remove(entry);
                UpdateCountText();
                UpdateCreateEnabled();
            };

            var nameStack = new StackPanel { Spacing = 2 };
            nameStack.Children.Add(name);
            nameStack.Children.Add(path);

            var row = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(icon, 0);
            Grid.SetColumn(nameStack, 1);
            Grid.SetColumn(removeBtn, 2);
            row.Children.Add(icon);
            row.Children.Add(nameStack);
            row.Children.Add(removeBtn);

            args.ItemContainer.ContentTemplate = null;
            args.ItemContainer.Content = row;
        };
        return this;
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (_dynamicFolderCheck.IsChecked == true)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            folderPicker.FileTypeFilter.Add("*");

            var h = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance!);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, h);

            var pickedFolder = await folderPicker.PickSingleFolderAsync();
            if (pickedFolder != null)
            {
                var folderName = System.IO.Path.GetFileName(pickedFolder.Path);
                if (string.IsNullOrWhiteSpace(folderName)) folderName = pickedFolder.Path;

                _files.Clear();
                _files.Add(new AppEntry
                {
                    Name = folderName,
                    ExePath = pickedFolder.Path,
                    IconPath = string.Empty
                });
                
                if (string.IsNullOrWhiteSpace(_nameBox.Text))
                {
                    _nameBox.Text = folderName;
                }

                UpdateCountText();
                UpdateCreateEnabled();
            }
            return;
        }

        var picker = new Windows.Storage.Pickers.FileOpenPicker
        {
            ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
        };
        // Accept all common file types
        foreach (var ext in new[] { ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt",
                                     ".txt", ".png", ".jpg", ".jpeg", ".mp4", ".mp3", ".zip",
                                     ".7z", ".exe", ".lnk", ".url", ".bat", ".cmd", "*" })
            picker.FileTypeFilter.Add(ext);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var picked = await picker.PickMultipleFilesAsync();
        if (picked == null || picked.Count == 0) return;

        foreach (var f in picked)
        {
            // Skip duplicates
            if (_files.Any(x => x.ExePath.Equals(f.Path, StringComparison.OrdinalIgnoreCase))) continue;

            var friendlyName = await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(f.Path);
                    if (!string.IsNullOrWhiteSpace(vi.ProductName)) return vi.ProductName.Trim();
                }
                catch { }
                return System.IO.Path.GetFileNameWithoutExtension(f.Path);
            });

            string cachedIcon = IconHelper.GetOrExtractIcon(f.Path);

            _files.Add(new AppEntry
            {
                Name    = friendlyName,
                ExePath = f.Path,
                IconPath = cachedIcon
            });
        }

        if (string.IsNullOrWhiteSpace(_nameBox.Text) && _files.Count > 0)
        {
            if (_files.Count == 1)
            {
                _nameBox.Text = _files[0].Name;
            }
            else
            {
                _nameBox.Text = "New File Group";
            }
        }

        UpdateCountText();
        UpdateCreateEnabled();
    }

    private void UpdateCountText()
    {
        if (_dynamicFolderCheck.IsChecked == true)
        {
            _countText.Text = _files.Count > 0 ? $"Folder selected: {_files[0].ExePath}" : "No folder selected";
            return;
        }

        _countText.Text = _files.Count switch
        {
            0 => "0 files selected",
            1 => "1 file selected",
            _ => $"{_files.Count} files selected"
        };
    }

    private void UpdateCreateEnabled()
    {
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(_nameBox.Text) && _files.Count > 0;
    }

    private void OnCreate(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true; // handle manually

        var name = _nameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || _files.Count == 0)
        {
            UpdateCreateEnabled();
            return;
        }

        bool isDyn = _dynamicFolderCheck.IsChecked == true;
        ResultGroup = new AppGroup
        {
            Name      = name,
            GroupType = GroupType.Files,
            IsDynamicFolder = isDyn,
            DynamicFolderPath = isDyn ? _files[0].ExePath : string.Empty,
            Apps      = _files.ToList()
        };
        Hide();
    }
}
