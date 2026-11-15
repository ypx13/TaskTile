using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace TaskTile.Popups
{
    public sealed class LaunchOptionsWindow : Window
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;

        private readonly Action<string, bool> _onSaved;
        private readonly Action? _onClosed;
        private TextBox _argsBox = null!;
        private ToggleSwitch _adminToggle = null!;

        public LaunchOptionsWindow(RectInt32 parentRect, string appName, string currentArgs, bool currentRunAsAdmin, Action<string, bool> onSaved, Action? onClosed = null)
        {
            _onSaved = onSaved;
            _onClosed = onClosed;

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
                SystemBackdrop = new MicaBackdrop();
            }
            catch { }

            BuildUI(appName, currentArgs, currentRunAsAdmin);
            PositionWindow(parentRect);

            this.Closed += (s, e) => _onClosed?.Invoke();
        }

        private void BuildUI(string appName, string currentArgs, bool currentRunAsAdmin)
        {
            _argsBox = new TextBox
            {
                Header = "Command-Line Arguments (CLI)",
                PlaceholderText = "e.g. -fullscreen -novid --profile=work",
                Text = currentArgs ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontSize = 13
            };

            _adminToggle = new ToggleSwitch
            {
                Header = "Always Run as Administrator",
                IsOn = currentRunAsAdmin,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 13
            };

            var saveBtn = new Button
            {
                Content = "Save",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                Width = 78,
                Height = 32
            };
            saveBtn.Click += (s, e) => SaveAndClose();

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Width = 78,
                Height = 32
            };
            cancelBtn.Click += (s, e) => this.Close();

            // Bottom row: ToggleSwitch and [Save] [Cancel] buttons on the exact same Y level
            var bottomGrid = new Grid
            {
                Margin = new Thickness(0, 2, 0, 0)
            };
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(_adminToggle, 0);
            bottomGrid.Children.Add(_adminToggle);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
                Children = { saveBtn, cancelBtn }
            };
            Grid.SetColumn(buttonPanel, 1);
            bottomGrid.Children.Add(buttonPanel);

            var headerPanel = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new FontIcon { Glyph = "\uE712", FontSize = 15, Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"] },
                            new TextBlock { Text = $"Launch Options - {appName}", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }
                        }
                    },
                    new TextBlock
                    {
                        Text = "Configure startup parameters and elevated privileges for this application.",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };

            var mainPanel = new StackPanel
            {
                Spacing = 10,
                Padding = new Thickness(16, 12, 16, 12),
                Children =
                {
                    headerPanel,
                    _argsBox,
                    bottomGrid
                }
            };

            var rootBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                Child = mainPanel
            };

            rootBorder.KeyDown += (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    SaveAndClose();
                    e.Handled = true;
                }
                else if (e.Key == Windows.System.VirtualKey.Escape)
                {
                    this.Close();
                    e.Handled = true;
                }
            };

            this.Content = rootBorder;
        }

        private void PositionWindow(RectInt32 parentRect)
        {
            int w = 410;
            int h = 180;

            int x = parentRect.X + (parentRect.Width - w) / 2;
            int y = parentRect.Y - h - 8;

            if (y < 40)
            {
                y = parentRect.Y + parentRect.Height + 8;
            }

            var hwnd = WindowNative.GetWindowHandle(this);
            var da = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd), DisplayAreaFallback.Primary);
            x = Math.Clamp(x, da.WorkArea.X + 16, da.WorkArea.X + da.WorkArea.Width - w - 16);
            y = Math.Clamp(y, da.WorkArea.Y + 16, da.WorkArea.Y + da.WorkArea.Height - h - 16);

            AppWindow.ResizeClient(new SizeInt32(w, h));
            AppWindow.Move(new PointInt32(x, y));
        }

        private void SaveAndClose()
        {
            string args = _argsBox.Text.Trim();
            bool admin = _adminToggle.IsOn;
            _onSaved?.Invoke(args, admin);
            this.Close();
        }
    }
}
