using System;
using System.Diagnostics;
using System.Windows;

namespace Golem_Mining_Suite
{
    public partial class UpdateAvailableWindow : Window
    {
        private UpdateInfo updateInfo;
        private bool isDownloading = false;

        public UpdateAvailableWindow(UpdateInfo info)
        {
            InitializeComponent();
            updateInfo = info;
            LoadUpdateInfo();
        }

        private void LoadUpdateInfo()
        {
            CurrentVersionText.Text = "v" + updateInfo.CurrentVersion;
            NewVersionText.Text = "v" + updateInfo.LatestVersion;

            if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
            {
                ReleaseNotesText.Text = updateInfo.ReleaseNotes;
            }
            else
            {
                ReleaseNotesText.Text = "No release notes available.";
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isDownloading)
                return;

            try
            {
                isDownloading = true;
                DownloadButton.IsEnabled = false;
                SkipButton.IsEnabled = false;

                // Check if we have a direct download URL
                if (string.IsNullOrEmpty(updateInfo.DownloadUrl) || 
                    !updateInfo.DownloadUrl.EndsWith(".exe"))
                {
                    // No .exe file attached, open browser instead
                    MessageBox.Show("No executable file found in the release.\n" +
                                   "Opening GitHub release page in browser...", 
                        "Manual Download Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updateInfo.ReleaseUrl,
                        UseShellExecute = true
                    });
                    
                    this.DialogResult = false;
                    this.Close();
                    return;
                }

                // Change button text to show downloading
                DownloadButton.Content = "Downloading... 0%";

                // Download and install the update
                bool success = await AutoUpdater.DownloadUpdateWithProgressAsync(
                    updateInfo, 
                    (progress) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            DownloadButton.Content = $"Downloading... {progress}%";
                        });
                    }
                );

                if (success)
                {
                    // AutoUpdater will close the app and restart with new version
                    // This code won't be reached
                }
                else
                {
                    DownloadButton.Content = "Download Update";
                    DownloadButton.IsEnabled = true;
                    SkipButton.IsEnabled = true;
                    isDownloading = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not download update: {ex.Message}\n\n" +
                               "Opening GitHub release page instead...", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Fallback to opening browser
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = updateInfo.DownloadUrl ?? updateInfo.ReleaseUrl,
                        UseShellExecute = true
                    });
                }
                catch { }

                this.DialogResult = false;
                this.Close();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
