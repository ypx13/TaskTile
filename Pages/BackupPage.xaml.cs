using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskTile.Controls;
using TaskTile.Models;
using TaskTile.Services;

namespace TaskTile.Pages
{
    public class GroupBackupItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AppsCountText { get; set; } = string.Empty;
    }

    public sealed partial class BackupPage : Page
    {
        private readonly ObservableCollection<GroupBackupItem> _groupItems = new();

        public BackupPage()
        {
            this.InitializeComponent();
            this.Loaded += BackupPage_Loaded;
        }

        private void BackupPage_Loaded(object sender, RoutedEventArgs e)
        {
            DestinationPathBox.Text = BackupService.Instance.GetDefaultBackupDirectory();

            _groupItems.Clear();
            foreach (var g in GroupService.Instance.Groups)
            {
                _groupItems.Add(new GroupBackupItem
                {
                    Id = g.Id.ToString(),
                    Name = string.IsNullOrWhiteSpace(g.Name) ? "Untitled Group" : g.Name,
                    AppsCountText = $"{g.Apps.Count} app{(g.Apps.Count == 1 ? "" : "s")}"
                });
            }
            GroupsListView.ItemsSource = _groupItems;
            GroupsListView.SelectAll();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            App.MainWindowInstance?.NavigateTo(typeof(TaskTile.Pages.SettingsPage));
        }

        private void BackupGroupsCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (GroupsSelectorCard != null) GroupsSelectorCard.Visibility = Visibility.Visible;
        }

        private void BackupGroupsCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (GroupsSelectorCard != null) GroupsSelectorCard.Visibility = Visibility.Collapsed;
        }

        private void SelectAllGroupsCheck_Click(object sender, RoutedEventArgs e)
        {
            if (SelectAllGroupsCheck.IsChecked == true)
            {
                GroupsListView.SelectAll();
            }
            else
            {
                GroupsListView.SelectedItems.Clear();
            }
        }

        private async void ChangeDestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    DestinationPathBox.Text = folder.Path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FolderPicker error: {ex.Message}");
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (BackupSettingsCheck.IsChecked != true &&
                BackupGroupsCheck.IsChecked != true &&
                BackupLaunchOptionsCheck.IsChecked != true &&
                BackupIconsCheck.IsChecked != true)
            {
                FeedbackInfoBar.Severity = InfoBarSeverity.Warning;
                FeedbackInfoBar.Title = "Nothing Selected";
                FeedbackInfoBar.Message = "Please select at least one component to include in the backup.";
                FeedbackInfoBar.IsOpen = true;
                return;
            }

            var selectedIds = new List<string>();
            if (BackupGroupsCheck.IsChecked == true)
            {
                foreach (var item in GroupsListView.SelectedItems)
                {
                    if (item is GroupBackupItem gItem)
                    {
                        selectedIds.Add(gItem.Id);
                    }
                }
            }

            var options = new BackupOptions
            {
                BackupSettings = BackupSettingsCheck.IsChecked == true,
                BackupGroups = BackupGroupsCheck.IsChecked == true,
                BackupLaunchOptions = BackupLaunchOptionsCheck.IsChecked == true,
                BackupIcons = BackupIconsCheck.IsChecked == true,
                SelectedGroupIds = selectedIds,
                OutputDirectory = DestinationPathBox.Text
            };

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            var dialog = new AnimatedBackupDialog(options, hwnd)
            {
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (!string.IsNullOrEmpty(dialog.GeneratedZipPath))
            {
                FeedbackInfoBar.Severity = InfoBarSeverity.Success;
                FeedbackInfoBar.Title = "Backup Saved";
                FeedbackInfoBar.Message = $"Archive placed in: {dialog.GeneratedZipPath}";
                FeedbackInfoBar.IsOpen = true;
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".zip");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    bool ok = await BackupService.Instance.RestoreBackupAsync(file.Path);
                    if (ok)
                    {
                        FeedbackInfoBar.Severity = InfoBarSeverity.Success;
                        FeedbackInfoBar.Title = "Restore Successful";
                        FeedbackInfoBar.Message = "Settings and app groups were restored successfully from the backup archive.";
                        FeedbackInfoBar.IsOpen = true;

                        // Refresh groups list on this page
                        BackupPage_Loaded(this, new RoutedEventArgs());
                    }
                    else
                    {
                        FeedbackInfoBar.Severity = InfoBarSeverity.Error;
                        FeedbackInfoBar.Title = "Restore Failed";
                        FeedbackInfoBar.Message = "The selected archive could not be restored. Please check if it is a valid TaskTile backup.";
                        FeedbackInfoBar.IsOpen = true;
                    }
                }
            }
            catch (Exception ex)
            {
                FeedbackInfoBar.Severity = InfoBarSeverity.Error;
                FeedbackInfoBar.Title = "Error";
                FeedbackInfoBar.Message = ex.Message;
                FeedbackInfoBar.IsOpen = true;
            }
        }
    }
}
