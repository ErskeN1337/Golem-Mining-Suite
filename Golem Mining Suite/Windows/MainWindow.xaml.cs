using System;
using System.Threading.Tasks;
using System.Windows;
using Golem_Mining_Suite.ViewModels;
using Golem_Mining_Suite.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite
{
    public partial class MainWindow : Window
    {
        private readonly UpdateChecker _updateChecker;
        private readonly AutoUpdater _autoUpdater;
        private readonly ILogger<MainWindow> _logger;

        public MainWindow(MainViewModel viewModel, UpdateChecker updateChecker, AutoUpdater autoUpdater, ILogger<MainWindow> logger)
        {
            InitializeComponent();
            DataContext = viewModel;
            _updateChecker = updateChecker;
            _autoUpdater = autoUpdater;
            _logger = logger;

            // Check for updates. The Loaded event delegate is void-returning, so we
            // bridge to the Task-returning CheckForUpdatesAsync via a handler that
            // converts any fault into a logged error rather than leaking an
            // unobserved async void exception.
            this.Loaded += (s, e) =>
            {
                _ = CheckForUpdatesAsync().ContinueWith(
                    t => _logger.LogError(t.Exception, "Update check on window load failed"),
                    TaskContinuationOptions.OnlyOnFaulted);
            };
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateInfo = await _updateChecker.CheckForUpdateAsync();
                if (updateInfo != null && updateInfo.IsUpdateAvailable)
                {
                    var updateWindow = new UpdateAvailableWindow(updateInfo, _autoUpdater);
                    updateWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    updateWindow.Owner = this;
                    updateWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update check failed");
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        public void PositionWindowToRight(Window window)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = this.Left + this.ActualWidth + 10;
            window.Top = this.Top;
        }
    }
}