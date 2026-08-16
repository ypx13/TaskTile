using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System;
using TaskTile.Services;
using TaskTile.Controls;
using Microsoft.UI.Windowing;
using System.IO.Pipes;
using System.IO;

namespace TaskTile;

public partial class App : Application
{
    public static MainWindow? MainWindowInstance { get; set; }

    // Single-instance mutex
    static Mutex? _mutex;

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "tasktile_crash.txt"),
                e.Exception.ToString());
        } catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var cmdArgs = Environment.GetCommandLineArgs();
        bool isPopup = false;
        string? groupId = null;
        for(int i=0; i<cmdArgs.Length; i++) {
            if (cmdArgs[i] == "--group" && i+1 < cmdArgs.Length) {
                isPopup = true;
                groupId = cmdArgs[i+1];
                break;
            }
        }

        if (isPopup && Guid.TryParse(groupId, out var parsedGuid))
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", "TaskTilePipe", PipeDirection.Out, PipeOptions.Asynchronous))
                {
                    client.Connect(100);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(groupId);
                        writer.Flush();
                    }
                }
                Environment.Exit(0);
                return;
            }
            catch
            {
                // Fallback: If no server is running, just launch directly
                var w = new TaskTile.Popups.PopupWindow(parsedGuid.ToString());
                w.Activate();
                return;
            }
        }

        // -- Single-instance guard (main window only) --
        _mutex = new Mutex(true, "TaskTile_SingleInstance_Mutex", out bool isNew);
        bool isReallyRunning = false;
        
        try
        {
            using (var client = new System.IO.Pipes.NamedPipeClientStream(".", "TaskTilePipe", System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous))
            {
                client.Connect(100);
                using (var writer = new System.IO.StreamWriter(client))
                {
                    writer.WriteLine("SHOW_MAIN");
                    writer.Flush();
                }
                isReallyRunning = true;
            }
        }
        catch { }
        
        if (!isNew && isReallyRunning)
        {
            Environment.Exit(0);
            return;
        }

        SettingsService.Load();

        // Pin to start if first run
        var s = SettingsService.Current;
        if (!s.FirstRunComplete && s.PinToStart)
        {
            PinToStartMenu();
            s.FirstRunComplete = true;
            SettingsService.Save();
        }

        // Launch main window
        var mw = new MainWindow();
        MainWindowInstance = mw;
        mw.Activate();

        // Run bar detection after window is ready (async, non-blocking)
        mw.DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(800); // let the window settle first
            var detection = await System.Threading.Tasks.Task.Run(() => BarDetectionService.Detect());

            bool isNew = detection.Detected &&
                         (detection.BarName != SettingsService.Current.LastDetectedBar
                          || !SettingsService.Current.HasAcknowledgedBarDetection);

            if (isNew && mw.Content?.XamlRoot != null)
            {
                // Warning dialog
                var warnTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                warnTitlePanel.Children.Add(new FontIcon { Glyph = "\uE7BA", Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 180, 0)) });
                warnTitlePanel.Children.Add(new TextBlock { Text = "Adjustment Needed", FontWeight = Microsoft.UI.Text.FontWeights.Bold });
                
                var warnDialog = new ContentDialog
                {
                    Title = warnTitlePanel,
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"A Windows modification has been detected:\n\n\u2022 {detection.BarName}\n\n{detection.Detail}\n\nIf this is a false positive, please report it in the Developer Sanctuary Discord server \"TaskTile - Fluent App Groups\" with the log located at:",
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                            },
                            new HyperlinkButton
                            {
                                Content = BarDetectionService.LogPath,
                                NavigateUri = new Uri("file://" + BarDetectionService.LogPath.Replace('\\', '/'))
                            }
                        }
                    },
                    PrimaryButtonText   = "Yes, I would like to adjust",
                    CloseButtonText     = "No",
                    DefaultButton       = ContentDialogButton.Close,
                    XamlRoot            = mw.Content.XamlRoot
                };

                ContentDialogResult warnResult = ContentDialogResult.None;
                for (int i = 0; i < 60; i++)
                {
                    try
                    {
                        warnResult = await warnDialog.ShowAsync();
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        // Another dialog is likely already open (e.g. changelog). Wait and retry.
                        await System.Threading.Tasks.Task.Delay(1000);
                    }
                }

                if (warnResult == ContentDialogResult.Primary)
                {
                    // Side picker dialog
                    var sideDialog = new ContentDialog
                    {
                        Title            = "Which side should app groups launch from?",
                        CloseButtonText  = "Cancel",
                        XamlRoot         = mw.Content.XamlRoot
                    };

                    var sideGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
                    sideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    sideGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                    var sides = new (string label, string glyph, Models.LaunchSide side, int row, int col, bool isExp)[]
                    {
                        ("Top",    "\uE70E", Models.LaunchSide.Top,    0, 0, false),
                        ("Bottom", "\uE70D", Models.LaunchSide.Bottom, 0, 1, false),
                        ("Left",   "\uE76B", Models.LaunchSide.Left,   1, 0, true),
                        ("Right",  "\uE76C", Models.LaunchSide.Right,  1, 1, true),
                    };

                    foreach (var s in sides)
                    {
                        var btn = new Button
                        {
                            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
                            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
                            MinHeight = 100,
                            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255)),
                            BorderThickness = new Thickness(1),
                            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(10, 255, 255, 255))
                        };
                        
                        var panel = new StackPanel { Spacing = 8, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center, VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center };
                        panel.Children.Add(new FontIcon { Glyph = s.glyph, FontSize = 32 });
                        panel.Children.Add(new TextBlock { Text = s.label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center });
                        if (s.isExp)
                        {
                            var tag = new Border
                            {
                                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(51, 255, 185, 0)),
                                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(128, 255, 185, 0)),
                                BorderThickness = new Thickness(1),
                                CornerRadius = new CornerRadius(4),
                                Padding = new Thickness(6, 2, 6, 2),
                                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center
                            };
                            tag.Child = new TextBlock
                            {
                                Text = "Experimental",
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                                FontSize = 10,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                            };
                            panel.Children.Add(tag);
                        }
                        btn.Content = panel;

                        var capSide = s.side;
                        btn.Click += async (_, _) =>
                        {
                            sideDialog.Hide();
                            SettingsService.Current.LaunchSide = capSide;
                            SettingsService.Current.HasAcknowledgedBarDetection = true;
                            SettingsService.Current.LastDetectedBar = detection.BarName;
                            SettingsService.Save();
                            mw.ApplySettings();
                            await System.Threading.Tasks.Task.CompletedTask;
                        };

                        Grid.SetRow(btn, s.row);
                        Grid.SetColumn(btn, s.col);
                        sideGrid.Children.Add(btn);
                    }
                    sideDialog.Content = sideGrid;
                    try
                    {
                        await sideDialog.ShowAsync();
                    }
                    catch { }
                }
                else
                {
                    SettingsService.Current.HasAcknowledgedBarDetection = true;
                    SettingsService.Current.LastDetectedBar = detection.BarName;
                    SettingsService.Save();
                }
            }
        });

        // Delay tray initialization slightly after activation
        _ = System.Threading.Tasks.Task.Run(() => {
            System.Threading.Thread.Sleep(1000);
            mw.DispatcherQueue.TryEnqueue(() => {
                if (SettingsService.Current.EnableTrayIcon)
                {
                    TaskTile.Helpers.SystemTrayManager.ShowSystemTray();
                }
            });
        });
    }

    static void PinToStartMenu()
    {
        System.Threading.Tasks.Task.Run(() => {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;
                var startMenu = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TaskTile");
                System.IO.Directory.CreateDirectory(startMenu);
                var lnkPath = System.IO.Path.Combine(startMenu, "TaskTile.lnk");
                if (System.IO.File.Exists(lnkPath)) return;
                var psScript = $@"
$ws = New-Object -ComObject WScript.Shell
$s  = $ws.CreateShortcut('{lnkPath.Replace("'", "''")}')
$s.TargetPath   = '{exePath.Replace("'", "''")}'
$s.Description  = 'TaskTile group launcher'
$s.Save()";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{psScript.Replace("\"", "\\\"")}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            } catch { }
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern short GetKeyState(int nVirtKey);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
