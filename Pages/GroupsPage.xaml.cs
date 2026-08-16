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

        this.Loaded += (_, _) => RefreshGroups();
        GroupService.Instance.Groups.CollectionChanged += (_, _) => RefreshGroups();
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
