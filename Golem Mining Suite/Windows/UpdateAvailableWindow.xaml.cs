using System;
using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite
{
    public partial class UpdateAvailableWindow : Window
    {
        private UpdateInfo updateInfo;
        private readonly AutoUpdater autoUpdater;
        private readonly ILogger<UpdateAvailableWindow>? _logger;
        private bool isDownloading = false;

        public UpdateAvailableWindow(UpdateInfo info, AutoUpdater autoUpdater)
        {
            InitializeComponent();
            updateInfo = info;
            this.autoUpdater = autoUpdater;
            // Best-effort logger resolution — this window is newed up manually rather
            // than via the DI container, so treat the logger as optional.
            _logger = App.Current?.Services?.GetService<ILogger<UpdateAvailableWindow>>();
            LoadUpdateInfo();
        }

        private void LoadUpdateInfo()
        {
            CurrentVersionText.Text = "v" + updateInfo.CurrentVersion;
            NewVersionText.Text = "v" + updateInfo.LatestVersion;

            if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
            {
                // Format release notes for better readability
                string formattedNotes = updateInfo.ReleaseNotes
                    .Replace("## ", "\n") // Remove markdown headers
                    .Replace("### ", "• ") // Convert subheaders to bullets
                    .Replace("- ", "  • ") // Indent list items
                    .Trim();

                ReleaseNotesText.Text = formattedNotes;
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

                // Check if we have a download URL
                if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    MessageBox.Show("No update file found in the release.\n" +
                                   "Opening GitHub release page in browser...",
                        "Manual Download Required", MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://github.com/ErskeN1337/Golem-Mining-Suite/releases/latest",
                        UseShellExecute = true
                    });

                    this.DialogResult = false;
                    this.Close();
                    return;
                }

                // Change button text to show downloading
                DownloadButton.Content = "Downloading... 0%";

                // Download and install the update - USE THIS METHOD
                bool success = await autoUpdater.DownloadUpdateWithProgressAsync(
                    updateInfo,
                    (progress) =>  // This is Action<int>, not IProgress<int>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (progress < 100)
                            {
                                DownloadButton.Content = $"Downloading... {progress}%";
                            }
                            else
                            {
                                DownloadButton.Content = "Installing...";
                            }
                        });
                    }
                );

                if (!success)
                {
                    DownloadButton.Content = "Download Update";
                    DownloadButton.IsEnabled = true;
                    SkipButton.IsEnabled = true;
                    isDownloading = false;
                }
                // If success, app will close and restart
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Update download failed; falling back to browser");
                MessageBox.Show($"Could not download update: {ex.Message}\n\n" +
                               "Opening GitHub release page instead...",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Fallback to opening browser
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://github.com/ErskeN1337/Golem-Mining-Suite/releases/latest",
                        UseShellExecute = true
                    });
                }
                catch (Exception browserEx)
                {
                    _logger?.LogWarning(browserEx, "Fallback browser launch also failed for GitHub releases page");
                }

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