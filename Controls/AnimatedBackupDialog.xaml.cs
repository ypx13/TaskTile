using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TaskTile.Services;

namespace TaskTile.Controls
{
    public sealed partial class AnimatedBackupDialog : ContentDialog
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly BackupOptions _options;
        private readonly IntPtr _mainWindowHwnd;
        private bool _isCancelled = false;

        public string? GeneratedZipPath { get; private set; }

        public AnimatedBackupDialog(BackupOptions options, IntPtr mainWindowHwnd)
        {
            this.InitializeComponent();
            _options = options;
            _mainWindowHwnd = mainWindowHwnd;

            this.Loaded += AnimatedBackupDialog_Loaded;
            this.PrimaryButtonClick += (s, e) => { /* Close */ };
        }

        private async void AnimatedBackupDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRunnerGif();
            await RunBackupAnimationAsync();
        }

        private void LoadRunnerGif()
        {
            try
            {
                string assetCropped = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "runner_cropped.gif");
                string assetGif = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "runner.gif");
                string userGif = @"C:\Users\yassinh\Pictures\Designs\ypx3D\2222.gif";

                string targetGif = File.Exists(assetCropped) ? assetCropped
                    : (File.Exists(assetGif) ? assetGif : userGif);

                if (File.Exists(targetGif))
                {
                    var bmp = new BitmapImage(new Uri(targetGif))
                    {
                        AutoPlay = true
                    };
                    RunnerGifImage.Source = bmp;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load runner GIF: {ex.Message}");
            }
        }

        private async Task RunBackupAnimationAsync()
        {
            try
            {
                IsPrimaryButtonEnabled = false;

                // ─── TRIP 1: Settings (TaskTile -> Backup) ───
                StatusDetailText.Text = "Transferring application settings & preferences...";
                await AnimateRunnerWalkAsync(targetX: 180, durationMs: 1400, facingRight: true, startPct: 0, endPct: 25);
                if (_isCancelled) return;

                // Deliver at Backup
                await SquishFolderAsync(BackupFolderScale);
                await Task.Delay(250);

                // ─── TRIP 2: Return to TaskTile (Backup -> TaskTile) ───
                StatusDetailText.Text = "Returning to fetch app groups & custom configurations...";
                await AnimateRunnerWalkAsync(targetX: 0, durationMs: 1300, facingRight: false, startPct: 25, endPct: 50);
                if (_isCancelled) return;

                // Arrive at TaskTile
                await SquishFolderAsync(TaskTileFolderScale);
                await Task.Delay(250);

                // ─── TRIP 3: App Groups & CLI Arguments (TaskTile -> Backup) ───
                StatusDetailText.Text = "Packaging app groups, custom CLI arguments & admin options...";
                await AnimateRunnerWalkAsync(targetX: 180, durationMs: 1400, facingRight: true, startPct: 50, endPct: 75);
                if (_isCancelled) return;

                // Deliver at Backup
                await SquishFolderAsync(BackupFolderScale);
                await Task.Delay(250);

                // ─── TRIP 4: Return to TaskTile (Backup -> TaskTile) ───
                StatusDetailText.Text = "Fetching custom icons & backup manifest...";
                await AnimateRunnerWalkAsync(targetX: 0, durationMs: 1300, facingRight: false, startPct: 75, endPct: 88);
                if (_isCancelled) return;

                // Arrive at TaskTile
                await SquishFolderAsync(TaskTileFolderScale);
                await Task.Delay(250);

                // ─── TRIP 5: Final Delivery (TaskTile -> Backup) ───
                StatusDetailText.Text = "Packing final backup bundle...";
                await AnimateRunnerWalkAsync(targetX: 180, durationMs: 1350, facingRight: true, startPct: 88, endPct: 95);
                if (_isCancelled) return;

                await SquishFolderAsync(BackupFolderScale);
                await Task.Delay(200);

                // Step back slightly to zip
                await AnimateRunnerWalkAsync(targetX: 155, durationMs: 350, facingRight: false, startPct: 95, endPct: 96);
                RunnerFlip.ScaleX = 1; // Face zipper

                StatusDetailText.Text = "Zipping up archive...";
                ZipperVisual.Visibility = Visibility.Visible;
                await AnimateZipperAsync();

                // Create actual ZIP file on disk in background
                GeneratedZipPath = await BackupService.Instance.CreateBackupAsync(_options);

                BackupFolderBorder.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                BackupFolderIcon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
                BackupFolderLabel.Text = "Backup.zip";
                BackupProgressBar.Value = 98;
                PercentageText.Text = "98%";
                await Task.Delay(250);

                // Launch and position File Explorer next to TaskTile
                BackupService.RECT explorerRect = new BackupService.RECT();
                if (!string.IsNullOrEmpty(GeneratedZipPath) && File.Exists(GeneratedZipPath))
                {
                    explorerRect = await BackupService.Instance.PositionExplorerNextToWindowAsync(GeneratedZipPath, _mainWindowHwnd);
                }

                await Task.Delay(150);

                // ─── THROW OUT OF TASKTILE INTO FILE EXPLORER ───
                StatusDetailText.Text = "Throwing archive to File Explorer — catch it!";
                
                RECT ttRect = new RECT { Left = 200, Top = 150, Right = 900, Bottom = 650 };
                if (_mainWindowHwnd != IntPtr.Zero)
                {
                    GetWindowRect(_mainWindowHwnd, out ttRect);
                }

                int startX = ttRect.Left + (ttRect.Right - ttRect.Left) / 2 + 100;
                int startY = ttRect.Top + (ttRect.Bottom - ttRect.Top) / 2;
                
                int endX = explorerRect.Right > explorerRect.Left
                    ? explorerRect.Left + (explorerRect.Right - explorerRect.Left) / 2
                    : ttRect.Right + 180;
                int endY = explorerRect.Bottom > explorerRect.Top
                    ? explorerRect.Top + (explorerRect.Bottom - explorerRect.Top) / 2
                    : startY;

                try
                {
                    var throwWin = new ThrowZipOverlayWindow(startX, startY, endX, endY);
                    throwWin.Activate();
                }
                catch { }

                await Task.Delay(750);

                BackupProgressBar.Value = 100;
                PercentageText.Text = "100%";
                StatusSubtext.Text = "Backup saved successfully!";
                StatusDetailText.Text = $"Caught in File Explorer: {Path.GetFileName(GeneratedZipPath ?? "TaskTile_Backup.zip")}";
                IsPrimaryButtonEnabled = true;
            }
            catch (Exception ex)
            {
                StatusDetailText.Text = $"Backup notice: {ex.Message}";
                IsPrimaryButtonEnabled = true;
            }
        }

        private async Task AnimateRunnerWalkAsync(double targetX, int durationMs, bool facingRight, double startPct, double endPct)
        {
            RunnerFlip.ScaleX = facingRight ? 1 : -1;
            double startX = RunnerTranslate.X;

            int steps = Math.Max(durationMs / 20, 20);
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double ease = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                RunnerTranslate.X = startX + (targetX - startX) * ease;
                RunnerTranslate.Y = Math.Sin(t * Math.PI * 6) * 2;

                double currentPct = startPct + (endPct - startPct) * t;
                BackupProgressBar.Value = currentPct;
                PercentageText.Text = $"{(int)Math.Round(currentPct)}%";

                await Task.Delay(durationMs / steps);
            }

            RunnerTranslate.X = targetX;
            RunnerTranslate.Y = 0;
            BackupProgressBar.Value = endPct;
            PercentageText.Text = $"{(int)Math.Round(endPct)}%";
        }

        private async Task SquishFolderAsync(ScaleTransform folderScale)
        {
            for (int i = 1; i <= 6; i++)
            {
                double scale = 1.0 + (0.16 * (1.0 - (double)i / 6));
                folderScale.ScaleX = scale;
                folderScale.ScaleY = scale;
                await Task.Delay(20);
            }
            folderScale.ScaleX = 1.0;
            folderScale.ScaleY = 1.0;
        }

        private async Task AnimateZipperAsync()
        {
            for (int i = 0; i <= 10; i++)
            {
                ZipperVisual.Margin = new Thickness(0, 4, 4 + i * 4, 0);
                await Task.Delay(35);
            }
        }
    }
}

// isnt she lovelyyyyyy
