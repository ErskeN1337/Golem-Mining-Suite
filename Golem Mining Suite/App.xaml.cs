using System;
using System.Threading.Tasks;
using System.Windows;

namespace Golem_Mining_Suite
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for updates before showing main window
            await CheckForUpdatesAsync();

            // Show main window
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Show a simple loading indicator (optional)
                // You could create a splash screen here if desired

                var updateInfo = await UpdateChecker.CheckForUpdatesAsync();

                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    // Show update available window
                    var updateWindow = new UpdateAvailableWindow(updateInfo);
                    updateWindow.ShowDialog(); // Blocks until user clicks Download or Skip
                }
            }
            catch (Exception ex)
            {
                // Silently fail - don't block app startup if update check fails
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }
    }
}
