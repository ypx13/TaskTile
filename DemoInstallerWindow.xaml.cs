using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI;

namespace TaskTile
{
    public sealed partial class DemoInstallerWindow : Window
    {
        public DemoInstallerWindow()
        {
            this.InitializeComponent();
            
            // Custom XAML titlebar override
            this.ExtendsContentIntoTitleBar = true;
            this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;
            this.SetTitleBar(AppTitleBar);
            
            // Set Mica backdrop
            this.SystemBackdrop = new TaskTile.Popups.MicaBackdropAlways();
            
            // Center window
            CenterWindow();
        }

        private void CenterWindow()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            
            // Set Window Icon
            try
            {
                var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "newicon.png");
                if (System.IO.File.Exists(iconPath))
                {
                    var bmp = new System.Drawing.Bitmap(iconPath);
                    var iconId = Microsoft.UI.Win32Interop.GetIconIdFromIcon(bmp.GetHicon());
                    appWindow.SetIcon(iconId);
                }
            } catch { }
            
            // Resize and center
            appWindow.Resize(new Windows.Graphics.SizeInt32(600, 480));
            
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                var x = workArea.X + (workArea.Width - 600) / 2;
                var y = workArea.Y + (workArea.Height - 480) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(x, y));
            }
        }

        private int _currentPhase = 1;

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            // Wait for user interaction to proceed through phases
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPhase == 1)
            {
                _currentPhase = 2;
                Phase1Grid.Visibility = Visibility.Collapsed;
                Phase2Grid.Visibility = Visibility.Visible;
            }
            else if (_currentPhase == 2)
            {
                _currentPhase = 3;
                Phase2Grid.Visibility = Visibility.Collapsed;
                Phase3Grid.Visibility = Visibility.Visible;

                // WebView2 properly handles VP9 alpha-transparent WebM
                await InstallerWebView.EnsureCoreWebView2Async();

                // Kill the right-click browser context menu
                InstallerWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                InstallerWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                // Force genuine transparency on the WebView2 surface (must be on the control, not CoreWebView2)
                InstallerWebView.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

                // Map Assets folder to a virtual host so video src works from NavigateToString
                var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
                InstallerWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "installer.local", assetsPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                InstallerWebView.NavigateToString(@"
<!DOCTYPE html><html><head><style>
  * { margin:0; padding:0; }
  html, body { background-color:rgba(0,0,0,0) !important; width:140px; height:140px; overflow:hidden; }
  video { display:block; width:140px; height:140px; object-fit:contain; }
</style></head><body>
  <video autoplay loop muted playsinline
         src='http://installer.local/installer_video.webm'></video>
</body></html>");
                
                NextButton.IsEnabled = false;
                CancelButton.IsEnabled = false;

                _ = StartFakeInstallAsync();
            }
            else if (_currentPhase == 3)
            {
                MainWindow mainWindow = new MainWindow();
                App.MainWindowInstance = mainWindow;
                mainWindow.Activate();
                this.Close();
            }
        }

        private async Task StartFakeInstallAsync()
        {
            for (int i = 0; i <= 100; i++)
            {
                InstallProgressBar.Value = i;
                PercentTextBlock.Text = $"{i}%";
                
                int delay = new Random().Next(10, 40); // random janky progress
                await Task.Delay(delay);
            }

            Phase3Title.Text = "Installation Complete!";
            NextButton.Content = "Launch TaskTile";
            NextButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            CancelButton.Content = "Close";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
            {
                op.Minimize();
            }
        }
    }
}
