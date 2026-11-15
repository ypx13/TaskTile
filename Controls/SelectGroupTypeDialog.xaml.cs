using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TaskTile.Models;

namespace TaskTile.Controls;

public sealed partial class SelectGroupTypeDialog : ContentDialog
{
    public GroupType? SelectedType { get; private set; }

    public SelectGroupTypeDialog()
    {
        this.InitializeComponent();
    }

    private void AppsButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = GroupType.Apps;
        this.Hide();
    }

    private void FilesButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = GroupType.Files;
        this.Hide();
    }

    private void RadialButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = GroupType.Radial;
        this.Hide();
    }
}
