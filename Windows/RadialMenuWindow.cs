using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using TaskTile.Helpers;
using TaskTile.Models;
using TaskTile.Services;

namespace TaskTile.Popups;

/// <summary>
/// A native WinUI 3 circular Pie / Radial menu that floats directly around the user's cursor.
/// Features:
/// - Floating icon-only layout (no background plate, no center dismiss button).
/// - Dynamic tight orbit spacing around cursor.
/// - Delayed train bloom entrance animation (staggered clockwise blossom with spring easing).
/// - Right-click context menu on app tiles (Open, Run as Admin, Open location, Edit group, Remove).
/// - Double-press trigger: launches all apps in a delayed train sequence with icon pulsing.
/// - Triple-press trigger: launches Edit Mode (dark remove X badges, + Add button, center Done pill).
/// - Auto-dismiss when an app is clicked or when cursor moves outside the icon hitbox.
/// </summary>
public sealed class RadialMenuWindow : Window
{
    private readonly AppGroup _group;
    private readonly Canvas _canvasRoot;
    private DispatcherQueueTimer? _hitboxTimer;
    private bool _isClosing = false;
    private bool _isFlyoutOpen = false;
    private bool _isEditMode = false;
    private DateTime _openedAt;

    private int _screenCenterX;
    private int _screenCenterY;
    private double _hitboxRadius;
    private int _winSize;

    private readonly List<Button> _itemButtons = new();

    public Guid GroupId => _group.Id;
    public bool IsEditMode => _isEditMode;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OPENFILENAME
    {
        public int lStructSize = 0;
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hInstance = IntPtr.Zero;
        public string? lpstrFilter = null;
        public string? lpstrCustomFilter = null;
        public int nMaxCustFilter = 0;
        public int nFilterIndex = 0;
        public string? lpstrFile = null;
        public int nMaxFile = 0;
        public string? lpstrFileTitle = null;
        public int nMaxFileTitle = 0;
        public string? lpstrInitialDir = null;
        public string? lpstrTitle = null;
        public int Flags = 0;
        public short nFileOffset = 0;
        public short nFileExtension = 0;
        public string? lpstrDefExt = null;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string? lpTemplateName = null;
        public IntPtr pvReserved = IntPtr.Zero;
        public int dwReserved = 0;
        public int FlagsEx = 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativeMethods.POINT pt, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OPENFILENAME ofn);

    public RadialMenuWindow(AppGroup group, bool isEditMode = false)
    {
        _group = group;
        _isEditMode = isEditMode;
        _openedAt = DateTime.UtcNow;

        // Window style: borderless, title-bar free, always on top
        ExtendsContentIntoTitleBar = true;
        if (AppWindow.TitleBar != null)
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        }

        if (AppWindow.Presenter is OverlappedPresenter op)
        {
            op.IsResizable = false;
            op.IsMaximizable = false;
            op.IsMinimizable = false;
            op.IsAlwaysOnTop = true;
            op.SetBorderAndTitleBar(false, false);
        }
        AppWindow.IsShownInSwitchers = false;

        // No backdrop -> transparent window frame
        SystemBackdrop = null;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        int dwmBackdrop = 1; // DWMSBT_NONE
        DwmSetWindowAttribute(hwnd, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref dwmBackdrop, sizeof(int));

        int colorNone = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        DwmSetWindowAttribute(hwnd, 34 /* DWMWA_BORDER_COLOR */, ref colorNone, sizeof(int));

        // Calculate dynamic dimensions
        CalculateDimensions(out double orbitRadius, out double itemSize);

        // Root container: transparent canvas
        _canvasRoot = new Canvas
        {
            Width = _winSize,
            Height = _winSize,
            // 1/255 alpha guarantees hit testing across DirectComposition while being completely invisible
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
        };

        _canvasRoot.PointerMoved += CanvasRoot_PointerMoved;
        _canvasRoot.PointerPressed += CanvasRoot_PointerPressed;
        _canvasRoot.DoubleTapped += CanvasRoot_DoubleTapped;
        _canvasRoot.KeyDown += CanvasRoot_KeyDown;

        this.Content = _canvasRoot;

        // Build floating icons layout with delayed train blossom animation
        BuildFloatingIconsUi(_winSize, orbitRadius, itemSize);

        // Position window centered exactly on cursor
        PositionAtCursor(_winSize);

        // Auto-dismiss on window deactivation
        this.Activated += RadialMenuWindow_Activated;

        // DispatcherQueueTimer polls cursor position every 25ms to detect when cursor leaves hitbox
        _hitboxTimer = this.DispatcherQueue.CreateTimer();
        _hitboxTimer.Interval = TimeSpan.FromMilliseconds(25);
        _hitboxTimer.Tick += HitboxTimer_Tick;
        _hitboxTimer.Start();
    }

    private void CalculateDimensions(out double orbitRadius, out double itemSize)
    {
        itemSize = 48.0;
        int n = _group.Apps.Count + (_isEditMode ? 1 : 0);
        double minRadius = 52.0;

        // Tight spacing: circumference needed is n * (itemSize + 10px spacing)
        double arcRadius = (n * (itemSize + 10.0)) / (2.0 * Math.PI);
        orbitRadius = Math.Max(minRadius, arcRadius);

        // Hitbox: outer edge of icons (orbitRadius + itemSize/2) plus 20px buffer
        _hitboxRadius = orbitRadius + (itemSize / 2.0) + 20.0;

        // Window size to contain the cluster comfortably
        _winSize = (int)Math.Ceiling((_hitboxRadius + 36.0) * 2.0);
        _winSize = Math.Max(280, _winSize);
    }

    private void HitboxTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_isClosing || _isFlyoutOpen || _isEditMode) return;
        // Grace period of 120ms so initial cursor placement doesn't trip dismissal
        if ((DateTime.UtcNow - _openedAt).TotalMilliseconds < 120) return;

        if (NativeMethods.GetCursorPos(out var pt))
        {
            double dx = pt.X - _screenCenterX;
            double dy = pt.Y - _screenCenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > _hitboxRadius)
            {
                Dismiss();
            }
        }
    }

    private void CanvasRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isClosing || _isFlyoutOpen || _isEditMode) return;
        if ((DateTime.UtcNow - _openedAt).TotalMilliseconds < 120) return;

        var pos = e.GetCurrentPoint(_canvasRoot).Position;
        double dx = pos.X - (_winSize / 2.0);
        double dy = pos.Y - (_winSize / 2.0);
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > _hitboxRadius)
        {
            Dismiss();
        }
    }

    private void CanvasRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Clicking transparent background dismisses unless in edit mode or flyout open
        if (!_isEditMode && !_isFlyoutOpen)
        {
            Dismiss();
        }
    }

    private void CanvasRoot_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!_isEditMode)
        {
            LaunchAllWithTrainAnim();
        }
    }

    private void RadialMenuWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            if (_isFlyoutOpen || _isEditMode) return;
            if ((DateTime.UtcNow - _openedAt).TotalMilliseconds > 250)
            {
                Dismiss();
            }
        }
    }

    private void CanvasRoot_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_isEditMode)
            {
                ExitEditMode();
            }
            else
            {
                Dismiss();
            }
            e.Handled = true;
        }
    }

    private void PositionAtCursor(int winSize)
    {
        NativeMethods.GetCursorPos(out var pt);
        int centerX = pt.X;
        int centerY = pt.Y;

        int x = centerX - winSize / 2;
        int y = centerY - winSize / 2;

        try
        {
            var hMon = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                int minX = mi.rcWork.left;
                int maxX = mi.rcWork.right - winSize;
                int minY = mi.rcWork.top;
                int maxY = mi.rcWork.bottom - winSize;

                x = Math.Clamp(x, minX, maxX);
                y = Math.Clamp(y, minY, maxY);
            }
        }
        catch { }

        AppWindow.MoveAndResize(new RectInt32(x, y, winSize, winSize));

        // Screen center point of the menu cluster
        _screenCenterX = x + winSize / 2;
        _screenCenterY = y + winSize / 2;
    }

    private void BuildFloatingIconsUi(int winSize, double orbitRadius, double itemSize)
    {
        double center = winSize / 2.0;
        _itemButtons.Clear();

        var apps = _group.Apps;

        // If in edit mode, show a small center pill indicating Edit Mode
        if (_isEditMode)
        {
            var centerPill = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = (Brush)Application.Current.Resources["SolidBackgroundFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 5, 8, 5)
            };

            var pillStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            pillStack.Children.Add(new FontIcon
            {
                Glyph = "\uE70F", // Pencil / Edit
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });

            pillStack.Children.Add(new TextBlock
            {
                Text = $"Editing {_group.Name}",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 90,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var doneBtn = new Button
            {
                Content = "Done",
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Padding = new Thickness(8, 2, 8, 2),
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Style = (Style)Application.Current.Resources["AccentButtonStyle"]
            };
            doneBtn.Click += (_, _) => ExitEditMode();
            pillStack.Children.Add(doneBtn);

            centerPill.Child = pillStack;
            centerPill.Measure(new Windows.Foundation.Size(200, 50));
            double pillW = 160;
            double pillH = 34;
            Canvas.SetLeft(centerPill, center - (pillW / 2.0));
            Canvas.SetTop(centerPill, center - (pillH / 2.0));
            _canvasRoot.Children.Add(centerPill);
        }

        if (!_isEditMode)
        {
            var centerAnchor = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                Opacity = 0.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(centerAnchor, center - 3);
            Canvas.SetTop(centerAnchor, center - 3);
            _canvasRoot.Children.Add(centerAnchor);
        }

        if (apps.Count == 0 && !_isEditMode)
        {
            // Empty state badge
            var emptyCard = new Border
            {
                Width = 140,
                Height = 44,
                CornerRadius = new CornerRadius(10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6)
            };
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new FontIcon
            {
                Glyph = "\uEB54",
                FontSize = 14,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
            });
            panel.Children.Add(new TextBlock
            {
                Text = "No apps in group",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            emptyCard.Child = panel;
            Canvas.SetLeft(emptyCard, center - 70);
            Canvas.SetTop(emptyCard, center - 22);
            _canvasRoot.Children.Add(emptyCard);
            return;
        }

        int totalItems = apps.Count + (_isEditMode ? 1 : 0);
        var entranceStoryboard = new Storyboard();

        for (int i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            // Distribute clockwise starting from 12 o'clock (-PI / 2)
            double angle = -Math.PI / 2.0 + (2.0 * Math.PI * i / totalItems);

            double targetX = center + orbitRadius * Math.Cos(angle) - (itemSize / 2.0);
            double targetY = center + orbitRadius * Math.Sin(angle) - (itemSize / 2.0);

            var (itemContainer, appBtn, scaleTrans, transTrans) = CreateFloatingAppItem(app, itemSize);
            _itemButtons.Add(appBtn);

            Canvas.SetLeft(itemContainer, targetX);
            Canvas.SetTop(itemContainer, targetY);
            _canvasRoot.Children.Add(itemContainer);

            // Staggered delayed train bloom entrance setup
            double deltaX = (center - (itemSize / 2.0)) - targetX;
            double deltaY = (center - (itemSize / 2.0)) - targetY;

            transTrans.X = deltaX;
            transTrans.Y = deltaY;
            scaleTrans.ScaleX = 0.15;
            scaleTrans.ScaleY = 0.15;
            itemContainer.Opacity = 0.0;

            // Delayed train stagger: each item begins i * 35ms after the previous
            var itemDelay = TimeSpan.FromMilliseconds(i * 35);
            var animDuration = TimeSpan.FromMilliseconds(220);
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

            var daTransX = new DoubleAnimation
            {
                From = deltaX,
                To = 0,
                BeginTime = itemDelay,
                Duration = new Duration(animDuration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(daTransX, transTrans);
            Storyboard.SetTargetProperty(daTransX, "X");
            entranceStoryboard.Children.Add(daTransX);

            var daTransY = new DoubleAnimation
            {
                From = deltaY,
                To = 0,
                BeginTime = itemDelay,
                Duration = new Duration(animDuration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(daTransY, transTrans);
            Storyboard.SetTargetProperty(daTransY, "Y");
            entranceStoryboard.Children.Add(daTransY);

            var daScaleX = new DoubleAnimation
            {
                From = 0.15,
                To = 1.0,
                BeginTime = itemDelay,
                Duration = new Duration(animDuration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(daScaleX, scaleTrans);
            Storyboard.SetTargetProperty(daScaleX, "ScaleX");
            entranceStoryboard.Children.Add(daScaleX);

            var daScaleY = new DoubleAnimation
            {
                From = 0.15,
                To = 1.0,
                BeginTime = itemDelay,
                Duration = new Duration(animDuration),
                EasingFunction = ease
            };
            Storyboard.SetTarget(daScaleY, scaleTrans);
            Storyboard.SetTargetProperty(daScaleY, "ScaleY");
            entranceStoryboard.Children.Add(daScaleY);

            var daOpacity = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                BeginTime = itemDelay,
                Duration = new Duration(TimeSpan.FromMilliseconds(160))
            };
            Storyboard.SetTarget(daOpacity, itemContainer);
            Storyboard.SetTargetProperty(daOpacity, "Opacity");
            entranceStoryboard.Children.Add(daOpacity);
        }

        // If in edit mode, add the "+ Add App" button as the final item in the orbit ring
        if (_isEditMode)
        {
            int addIndex = apps.Count;
            double angle = -Math.PI / 2.0 + (2.0 * Math.PI * addIndex / totalItems);
            double targetX = center + orbitRadius * Math.Cos(angle) - (itemSize / 2.0);
            double targetY = center + orbitRadius * Math.Sin(angle) - (itemSize / 2.0);

            var (addContainer, addBtn, scaleTrans, transTrans) = CreateAddAppItem(itemSize);

            Canvas.SetLeft(addContainer, targetX);
            Canvas.SetTop(addContainer, targetY);
            _canvasRoot.Children.Add(addContainer);

            double deltaX = (center - (itemSize / 2.0)) - targetX;
            double deltaY = (center - (itemSize / 2.0)) - targetY;

            transTrans.X = deltaX;
            transTrans.Y = deltaY;
            scaleTrans.ScaleX = 0.15;
            scaleTrans.ScaleY = 0.15;
            addContainer.Opacity = 0.0;

            var itemDelay = TimeSpan.FromMilliseconds(addIndex * 35);
            var animDuration = TimeSpan.FromMilliseconds(220);
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

            var daTransX = new DoubleAnimation { From = deltaX, To = 0, BeginTime = itemDelay, Duration = new Duration(animDuration), EasingFunction = ease };
            Storyboard.SetTarget(daTransX, transTrans); Storyboard.SetTargetProperty(daTransX, "X"); entranceStoryboard.Children.Add(daTransX);

            var daTransY = new DoubleAnimation { From = deltaY, To = 0, BeginTime = itemDelay, Duration = new Duration(animDuration), EasingFunction = ease };
            Storyboard.SetTarget(daTransY, transTrans); Storyboard.SetTargetProperty(daTransY, "Y"); entranceStoryboard.Children.Add(daTransY);

            var daScaleX = new DoubleAnimation { From = 0.15, To = 1.0, BeginTime = itemDelay, Duration = new Duration(animDuration), EasingFunction = ease };
            Storyboard.SetTarget(daScaleX, scaleTrans); Storyboard.SetTargetProperty(daScaleX, "ScaleX"); entranceStoryboard.Children.Add(daScaleX);

            var daScaleY = new DoubleAnimation { From = 0.15, To = 1.0, BeginTime = itemDelay, Duration = new Duration(animDuration), EasingFunction = ease };
            Storyboard.SetTarget(daScaleY, scaleTrans); Storyboard.SetTargetProperty(daScaleY, "ScaleY"); entranceStoryboard.Children.Add(daScaleY);

            var daOpacity = new DoubleAnimation { From = 0.0, To = 1.0, BeginTime = itemDelay, Duration = new Duration(TimeSpan.FromMilliseconds(160)) };
            Storyboard.SetTarget(daOpacity, addContainer); Storyboard.SetTargetProperty(daOpacity, "Opacity"); entranceStoryboard.Children.Add(daOpacity);
        }

        entranceStoryboard.Begin();
    }

    private (Grid container, Button button, ScaleTransform scale, TranslateTransform trans) CreateFloatingAppItem(AppEntry app, double size)
    {
        var container = new Grid
        {
            Width = size,
            Height = size
        };

        var btn = new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        ToolTipService.SetToolTip(btn, app.Name);

        var contentGrid = new Grid
        {
            Width = size,
            Height = size
        };

        var img = new Image
        {
            Width = 28,
            Height = 28,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Load Icon
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            string? iconPath = null;
            if (!string.IsNullOrEmpty(app.IconPath) && File.Exists(app.IconPath))
                iconPath = app.IconPath;
            else if (!string.IsNullOrEmpty(app.ExePath))
                iconPath = IconHelper.GetOrExtractIcon(app.ExePath);

            return iconPath;
        }).ContinueWith(t =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (!string.IsNullOrEmpty(t.Result) && File.Exists(t.Result))
                {
                    try
                    {
                        var bi = new BitmapImage(new Uri(t.Result));
                        img.Source = bi;
                    }
                    catch { }
                }
                else
                {
                    contentGrid.Children.Clear();
                    contentGrid.Children.Add(new FontIcon
                    {
                        Glyph = "\uE71D",
                        FontSize = 20,
                        Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                    });
                }
            });
        });

        contentGrid.Children.Add(img);
        btn.Content = contentGrid;

        // Context menu (Right Click)
        var contextMenu = CreateAppContextMenu(app);
        btn.ContextFlyout = contextMenu;

        // Transform Group supporting both Blossom entrance and Hover scaling
        var transformGroup = new TransformGroup();
        var scaleTrans = new ScaleTransform { CenterX = size / 2.0, CenterY = size / 2.0, ScaleX = 1.0, ScaleY = 1.0 };
        var transTrans = new TranslateTransform();
        transformGroup.Children.Add(scaleTrans);
        transformGroup.Children.Add(transTrans);
        container.RenderTransform = transformGroup;

        // Hover scale and color states
        btn.PointerEntered += (_, _) =>
        {
            btn.BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"];
            btn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];

            var sb = new Storyboard();
            var daX = new DoubleAnimation { To = 1.06, Duration = new Duration(TimeSpan.FromMilliseconds(120)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var daY = new DoubleAnimation { To = 1.06, Duration = new Duration(TimeSpan.FromMilliseconds(120)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(daX, scaleTrans); Storyboard.SetTargetProperty(daX, "ScaleX");
            Storyboard.SetTarget(daY, scaleTrans); Storyboard.SetTargetProperty(daY, "ScaleY");
            sb.Children.Add(daX); sb.Children.Add(daY);
            sb.Begin();
        };

        btn.PointerExited += (_, _) =>
        {
            btn.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            btn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];

            var sb = new Storyboard();
            var daX = new DoubleAnimation { To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(120)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var daY = new DoubleAnimation { To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(120)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(daX, scaleTrans); Storyboard.SetTargetProperty(daX, "ScaleX");
            Storyboard.SetTarget(daY, scaleTrans); Storyboard.SetTargetProperty(daY, "ScaleY");
            sb.Children.Add(daX); sb.Children.Add(daY);
            sb.Begin();
        };

        // Launch app on click and auto-dismiss (unless in edit mode)
        btn.Click += (_, _) =>
        {
            if (_isEditMode) return;
            LaunchApp(app);
            Dismiss();
        };

        container.Children.Add(btn);

        // Edit Mode Remove Badge (Top-Right)
        if (_isEditMode)
        {
            var removeBtn = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                CornerRadius = new CornerRadius(6),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
                BorderThickness = new Thickness(1),
                Visibility = Visibility.Visible
            };
            ToolTipService.SetToolTip(removeBtn, $"Remove {app.Name}");
            removeBtn.Content = new FontIcon
            {
                Glyph = "\uE711",
                FontSize = 8,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            removeBtn.Click += (s, e) =>
            {
                RemoveApp(app);
            };
            container.Children.Add(removeBtn);
        }

        return (container, btn, scaleTrans, transTrans);
    }

    private (Grid container, Button button, ScaleTransform scale, TranslateTransform trans) CreateAddAppItem(double size)
    {
        var container = new Grid
        {
            Width = size,
            Height = size
        };

        var btn = new Button
        {
            Width = size,
            Height = size,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"],
            BorderThickness = new Thickness(1.5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        ToolTipService.SetToolTip(btn, "Add application to radial menu");

        btn.Content = new FontIcon
        {
            Glyph = "\uE710", // Plus / Add
            FontSize = 18,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
        };

        btn.Click += (_, _) => ShowAddAppMenu(btn);

        var transformGroup = new TransformGroup();
        var scaleTrans = new ScaleTransform { CenterX = size / 2.0, CenterY = size / 2.0, ScaleX = 1.0, ScaleY = 1.0 };
        var transTrans = new TranslateTransform();
        transformGroup.Children.Add(scaleTrans);
        transformGroup.Children.Add(transTrans);
        container.RenderTransform = transformGroup;

        btn.PointerEntered += (_, _) =>
        {
            btn.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            btn.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];

            var sb = new Storyboard();
            var daX = new DoubleAnimation { To = 1.15, Duration = new Duration(TimeSpan.FromMilliseconds(90)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var daY = new DoubleAnimation { To = 1.15, Duration = new Duration(TimeSpan.FromMilliseconds(90)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(daX, scaleTrans); Storyboard.SetTargetProperty(daX, "ScaleX");
            Storyboard.SetTarget(daY, scaleTrans); Storyboard.SetTargetProperty(daY, "ScaleY");
            sb.Children.Add(daX); sb.Children.Add(daY);
            sb.Begin();
        };

        btn.PointerExited += (_, _) =>
        {
            btn.BorderBrush = (Brush)Application.Current.Resources["ControlElevationBorderBrush"];
            btn.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

            var sb = new Storyboard();
            var daX = new DoubleAnimation { To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(90)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var daY = new DoubleAnimation { To = 1.0, Duration = new Duration(TimeSpan.FromMilliseconds(90)), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(daX, scaleTrans); Storyboard.SetTargetProperty(daX, "ScaleX");
            Storyboard.SetTarget(daY, scaleTrans); Storyboard.SetTargetProperty(daY, "ScaleY");
            sb.Children.Add(daX); sb.Children.Add(daY);
            sb.Begin();
        };

        container.Children.Add(btn);
        return (container, btn, scaleTrans, transTrans);
    }

    private MenuFlyout CreateAppContextMenu(AppEntry app)
    {
        var menu = new MenuFlyout();

        // 1. Open
        var openItem = new MenuFlyoutItem { Text = "Open" };
        openItem.Icon = new FontIcon { Glyph = "\uE768" };
        openItem.Click += (_, _) => { LaunchApp(app); Dismiss(); };
        menu.Items.Add(openItem);

        // 2. Run as administrator
        var adminItem = new MenuFlyoutItem { Text = "Run as administrator" };
        adminItem.Icon = new FontIcon { Glyph = "\uE7EF" };
        adminItem.Click += (_, _) => { LaunchAppAsAdmin(app); Dismiss(); };
        menu.Items.Add(adminItem);

        // 3. Open file location
        if (!string.IsNullOrEmpty(app.ExePath))
        {
            var locItem = new MenuFlyoutItem { Text = "Open file location" };
            locItem.Icon = new FontIcon { Glyph = "\uE838" };
            locItem.Click += (_, _) => { OpenFileLocation(app.ExePath); Dismiss(); };
            menu.Items.Add(locItem);
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        // 4. Edit group (toggles edit mode)
        var editItem = new MenuFlyoutItem { Text = _isEditMode ? "Done editing" : "Edit group" };
        editItem.Icon = new FontIcon { Glyph = "\uE70F" };
        editItem.Click += (_, _) =>
        {
            ToggleEditMode();
        };
        menu.Items.Add(editItem);

        // 5. Remove from radial menu
        var removeItem = new MenuFlyoutItem { Text = "Remove from radial menu" };
        removeItem.Icon = new FontIcon { Glyph = "\uE74D" };
        removeItem.Click += (_, _) => { RemoveApp(app); };
        menu.Items.Add(removeItem);

        // 6. Full settings in TaskTile
        var settingsItem = new MenuFlyoutItem { Text = "Group settings in TaskTile" };
        settingsItem.Icon = new FontIcon { Glyph = "\uE713" };
        settingsItem.Click += (_, _) =>
        {
            Dismiss();
            OpenGroupSettingsInTaskTile();
        };
        menu.Items.Add(settingsItem);

        menu.Opening += (_, _) => _isFlyoutOpen = true;
        menu.Closed += (_, _) =>
        {
            _isFlyoutOpen = false;
            _openedAt = DateTime.UtcNow;
        };

        return menu;
    }

    private void ShowAddAppMenu(FrameworkElement target)
    {
        _isFlyoutOpen = true;
        var flyout = new MenuFlyout();

        var installedItem = new MenuFlyoutItem { Text = "Installed app..." };
        installedItem.Icon = new FontIcon { Glyph = "\uE71D" };
        installedItem.Click += (_, _) => ShowInstalledAppsPicker();
        flyout.Items.Add(installedItem);

        var browseItem = new MenuFlyoutItem { Text = "Browse for file..." };
        browseItem.Icon = new FontIcon { Glyph = "\uE838" };
        browseItem.Click += (_, _) => ShowFilePicker();
        flyout.Items.Add(browseItem);

        flyout.Closed += (_, _) =>
        {
            _isFlyoutOpen = false;
            _openedAt = DateTime.UtcNow;
        };

        flyout.ShowAt(target);
    }

    private void ShowInstalledAppsPicker()
    {
        try
        {
            _isFlyoutOpen = true;
            var parentRect = new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
            var pickerWin = new InstalledAppsPickerWindow(
                parentRect,
                onAppSelected: (entry) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _isFlyoutOpen = false;
                        _openedAt = DateTime.UtcNow;
                        AddAppToGroup(entry.Name, entry.ExePath);
                    });
                },
                onBrowseFile: () =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ShowFilePicker();
                    });
                });

            pickerWin.Closed += (_, _) =>
            {
                _isFlyoutOpen = false;
                _openedAt = DateTime.UtcNow;
            };
            pickerWin.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadialMenuWindow] Error opening installed apps picker: {ex.Message}");
            ShowFilePicker();
        }
    }

    private void ShowFilePicker()
    {
        _isFlyoutOpen = true;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            var picked = PickExecutableWin32(hwnd);
            DispatcherQueue.TryEnqueue(() =>
            {
                _isFlyoutOpen = false;
                _openedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(picked) && File.Exists(picked))
                {
                    var name = Path.GetFileNameWithoutExtension(picked);
                    AddAppToGroup(name, picked);
                }
            });
        });
    }

    private static string? PickExecutableWin32(IntPtr hwnd)
    {
        var ofn = new OPENFILENAME();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.hwndOwner = hwnd;
        ofn.lpstrFilter = "Applications & Shortcuts (*.exe;*.lnk;*.url;*.bat;*.cmd;*.msi)\0*.exe;*.lnk;*.url;*.bat;*.cmd;*.msi\0All Files (*.*)\0*.*\0\0";
        ofn.lpstrFile = new string(new char[2048]);
        ofn.nMaxFile = 2048;
        ofn.lpstrTitle = "Select an Application or Shortcut for Radial Menu";
        ofn.Flags = 0x00001000 | 0x00000800 | 0x00080000 | 0x00000008;

        if (GetOpenFileName(ofn))
        {
            return ofn.lpstrFile?.TrimEnd('\0');
        }
        return null;
    }

    private void AddAppToGroup(string name, string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return;
        var newEntry = new AppEntry
        {
            Name = name,
            ExePath = exePath,
            TileSize = 1
        };
        _group.Apps.Add(newEntry);
        GroupService.Instance.Save();
        RebuildUi();
    }

    private void RemoveApp(AppEntry app)
    {
        _group.Apps.Remove(app);
        GroupService.Instance.Save();
        RebuildUi();
    }

    private void RebuildUi()
    {
        _canvasRoot.Children.Clear();
        CalculateDimensions(out double orbitRadius, out double itemSize);
        BuildFloatingIconsUi(_winSize, orbitRadius, itemSize);
    }

    public void ToggleEditMode()
    {
        if (_isEditMode)
            ExitEditMode();
        else
            EnterEditMode();
    }

    public void EnterEditMode()
    {
        if (_isEditMode) return;
        _isEditMode = true;
        _openedAt = DateTime.UtcNow;
        RebuildUi();
    }

    public void ExitEditMode()
    {
        if (!_isEditMode) return;
        _isEditMode = false;
        _openedAt = DateTime.UtcNow;
        RebuildUi();
    }

    /// <summary>
    /// Launches all apps in a delayed train sequence with sequential icon pulse animations.
    /// </summary>
    public void LaunchAllWithTrainAnim()
    {
        if (_isClosing) return;
        _isClosing = true;
        _hitboxTimer?.Stop();

        var apps = _group.Apps.ToList();
        if (apps.Count == 0)
        {
            Dismiss();
            return;
        }

        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            for (int i = 0; i < apps.Count; i++)
            {
                var app = apps[i];
                int index = i;

                // Pulse the icon on UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    PulseAppButton(index);
                });

                // Launch process
                LaunchApp(app);

                if (i < apps.Count - 1)
                {
                    await System.Threading.Tasks.Task.Delay(120); // Delayed train stagger
                }
            }

            await System.Threading.Tasks.Task.Delay(260);
            DispatcherQueue.TryEnqueue(() =>
            {
                try { RadialMenuManager.CloseCurrent(); } catch { }
            });
        });
    }

    private void PulseAppButton(int index)
    {
        if (index < 0 || index >= _itemButtons.Count) return;
        var btn = _itemButtons[index];
        btn.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        btn.Background = (Brush)Application.Current.Resources["AccentFillColorSecondaryBrush"];

        if (btn.Parent is Grid grid && grid.RenderTransform is TransformGroup tg && tg.Children.Count > 0 && tg.Children[0] is ScaleTransform st)
        {
            var sb = new Storyboard();
            var daX = new DoubleAnimation { To = 1.25, Duration = new Duration(TimeSpan.FromMilliseconds(110)), AutoReverse = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var daY = new DoubleAnimation { To = 1.25, Duration = new Duration(TimeSpan.FromMilliseconds(110)), AutoReverse = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(daX, st); Storyboard.SetTargetProperty(daX, "ScaleX");
            Storyboard.SetTarget(daY, st); Storyboard.SetTargetProperty(daY, "ScaleY");
            sb.Children.Add(daX); sb.Children.Add(daY);
            sb.Begin();
        }
    }

    private static void LaunchApp(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.ExePath)) return;
        bool isShift = TaskTile.Services.AppRunningService.IsShiftPressed();
        if (!isShift && TaskTile.Services.AppRunningService.IsAppRunning(app.ExePath))
        {
            if (TaskTile.Services.AppRunningService.TryActivateRunningInstance(app.ExePath))
            {
                return;
            }
        }

        try
        {
            var psi = new ProcessStartInfo(app.ExePath)
            {
                UseShellExecute = true
            };

            try
            {
                if (File.Exists(app.ExePath) || Directory.Exists(app.ExePath))
                {
                    var dir = Path.GetDirectoryName(app.ExePath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        psi.WorkingDirectory = dir;
                }
            }
            catch { }

            var proc = Process.Start(psi);
            if (proc != null)
            {
                TaskTile.Services.AppRunningService.RegisterLaunchedProcess(app.ExePath, proc.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadialMenuWindow] Launch error for {app.Name}: {ex.Message}");
        }
    }

    private static void LaunchAppAsAdmin(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.ExePath)) return;
        try
        {
            var psi = new ProcessStartInfo(app.ExePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RadialMenuWindow] Admin launch error for {app.Name}: {ex.Message}");
        }
    }

    private static void OpenFileLocation(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }

    private void OpenGroupSettingsInTaskTile()
    {
        try
        {
            if (App.MainWindowInstance != null)
            {
                App.MainWindowInstance.DispatcherQueue.TryEnqueue(() =>
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                    App.ShowWindow(hwnd, 9); // SW_RESTORE
                    App.SetForegroundWindow(hwnd);
                    App.MainWindowInstance.NavigateTo(typeof(TaskTile.Pages.GroupsPage));
                });
            }
        }
        catch { }
    }

    private void Dismiss()
    {
        if (_isClosing) return;
        _isClosing = true;

        try
        {
            _hitboxTimer?.Stop();
            _hitboxTimer = null;
        }
        catch { }

        try
        {
            RadialMenuManager.CloseCurrent();
        }
        catch { }
    }
}
