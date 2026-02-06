using System;
using System.Diagnostics;
using System.Windows;

namespace Golem_Mining_Suite
{
    public partial class UpdateAvailableWindow : Window
    {
        private UpdateInfo updateInfo;

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

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the download URL in default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = updateInfo.DownloadUrl ?? updateInfo.ReleaseUrl,
                    UseShellExecute = true
                });

                // Close the update window
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open download page: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
