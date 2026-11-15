using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using WinRT.Interop;

namespace TaskTile.Popups;

/// <summary>
/// A detached floating Glow UI rename dialog window.
/// Floats smoothly centered on top of the app group card with no close ('X') button,
/// containing clean, luminous Glow UI rename controls.
/// </summary>
public sealed class FloatingRenameWindow : Window
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);

    private readonly string _initialName;
    private readonly Action<string> _onSave;
    private TextBox _renameBox = null!;
    private Grid _rootGrid = null!;
    private Border _bubbleCard = null!;
    private ScaleTransform _scaleTransform = null!;

    public FloatingRenameWindow(RectInt32 parentRect, string currentName, Action<string> onSave)
    {
        _initialName = currentName ?? string.Empty;
        _onSave = onSave;

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

        this.Activated += FloatingRenameWindow_Activated;
    }

    private void FloatingRenameWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _renameBox?.SelectAll();
            _renameBox?.Focus(FocusState.Programmatic);
        }
    }

    private void PositionWindow(RectInt32 parentRect)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        uint dpi = GetDpiForWindow(hwnd);
        double scale = (dpi > 0) ? dpi / 96.0 : 1.0;

        int w = (int)Math.Round(290 * scale);
        int h = (int)Math.Round(140 * scale);

        int targetX = parentRect.X + (parentRect.Width - w) / 2;
        int targetY = parentRect.Y + (parentRect.Height - h) / 2;

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var work = displayArea.WorkArea;
                targetX = Math.Clamp(targetX, work.X + 16, work.X + work.Width - w - 16);
                targetY = Math.Clamp(targetY, work.Y + 16, work.Y + work.Height - h - 16);
            }
        }
        catch { }

        if (targetX < 20) targetX = 20;
        if (targetY < 20) targetY = 20;

        AppWindow.MoveAndResize(new RectInt32(targetX, targetY, w, h));
    }

    private void BuildUI()
    {
        _rootGrid = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Opacity = 0.0
        };

        // Clean floating Glow UI card with FireCube dynamic System Accent glow
        _bubbleCard = new Border
        {
            Background = TaskTile.Helpers.GlowUITheme.CreateCardBackgroundBrush(),
            BorderBrush = TaskTile.Helpers.GlowUITheme.CreateGlowBorderBrush(false),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18, 14, 18, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _scaleTransform = new ScaleTransform { CenterX = 145, CenterY = 70, ScaleX = 0.92, ScaleY = 0.92 };
        _bubbleCard.RenderTransform = _scaleTransform;

        var contentStack = new StackPanel { Spacing = 12 };

        // Header: Icon + Title, explicitly NO 'X' button
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.Children.Add(new FontIcon
        {
            Glyph = "\uE8AC",
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = "Rename Group",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        contentStack.Children.Add(header);

        // FireCube signature Accent Glow Pipe
        var pipe = new Grid
        {
            Height = 2,
            CornerRadius = new CornerRadius(1),
            Background = TaskTile.Helpers.GlowUITheme.CreateAccentPipeBrush(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(2, -4, 2, 0)
        };
        contentStack.Children.Add(pipe);

        // Rename TextBox
        _renameBox = new TextBox
        {
            Text = _initialName,
            PlaceholderText = "New group name...",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(8),
            IsSpellCheckEnabled = false
        };
        _renameBox.KeyDown += RenameBox_KeyDown;
        contentStack.Children.Add(_renameBox);

        // Action Buttons: Cancel and Save
        var buttonsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelBtn = new Button
        {
            Content = "Cancel",
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 5, 14, 5)
        };
        cancelBtn.Click += (_, _) => this.Close();
        buttonsRow.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            Content = "Save",
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 5, 18, 5),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        saveBtn.Click += (_, _) => DoSave();
        buttonsRow.Children.Add(saveBtn);

        contentStack.Children.Add(buttonsRow);
        _bubbleCard.Child = contentStack;
        _rootGrid.Children.Add(_bubbleCard);

        // Smooth pop-in animation
        var sbIn = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var daOp = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daOp, _rootGrid);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daOp, "Opacity");
        sbIn.Children.Add(daOp);

        var daScaleX = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0.92,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.BackEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daScaleX, _scaleTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daScaleX, "ScaleX");
        sbIn.Children.Add(daScaleX);

        var daScaleY = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0.92,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromMilliseconds(160)),
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.BackEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut, Amplitude = 0.3 }
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(daScaleY, _scaleTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(daScaleY, "ScaleY");
        sbIn.Children.Add(daScaleY);

        _rootGrid.Loaded += (_, _) => sbIn.Begin();

        this.Content = _rootGrid;
    }

    private void RenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            DoSave();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            this.Close();
            e.Handled = true;
        }
    }

    private void DoSave()
    {
        var text = _renameBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            _onSave?.Invoke(text);
        }
        this.Close();
    }
}
