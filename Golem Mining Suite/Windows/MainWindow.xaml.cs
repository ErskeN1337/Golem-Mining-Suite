using System;
using System.Threading.Tasks;
using System.Windows;
using Golem_Mining_Suite.Views;

namespace Golem_Mining_Suite
{
	public partial class MainWindow : Window
	{
		private MainMenuView mainMenuView;
		private SurfaceMiningView surfaceMiningView;
		private AsteroidMiningView asteroidMiningView; 

		public MainWindow()
		{
			InitializeComponent();

			// Initialize views
			mainMenuView = new MainMenuView();
			mainMenuView.NavigationRequested += OnNavigationRequested;

			surfaceMiningView = new SurfaceMiningView();
			surfaceMiningView.BackToMenuRequested += OnBackToMenuRequested;

			asteroidMiningView = new AsteroidMiningView();  
			asteroidMiningView.BackToMenuRequested += OnBackToMenuRequested;

			// Show main menu by default
			ShowMainMenu();

			// Check for updates
			this.Loaded += Window_Loaded;
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			await CheckForUpdatesAsync();
		}

		private async Task CheckForUpdatesAsync()
		{
			try
			{
				var updateInfo = await UpdateChecker.CheckForUpdateAsync();

				if (updateInfo != null && updateInfo.IsUpdateAvailable)
				{
					var updateWindow = new UpdateAvailableWindow(updateInfo);
					PositionWindowToRight(updateWindow);
					updateWindow.Owner = this;
					updateWindow.ShowDialog();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
			}
		}

		private void OnNavigationRequested(object sender, string destination)
		{
			switch (destination)
			{
				case "SurfaceMining":
					ShowSurfaceMining();
					break;
				case "AsteroidMining":
					ShowAsteroidMining();  
					break;
				case "RocMining":
					MessageBox.Show("ROC/FPS Mining - Coming Soon!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
					break;
			}
		}

		private void OnBackToMenuRequested(object sender, EventArgs e)
		{
			ShowMainMenu();
		}

		private void ShowMainMenu()
		{
			ContentArea.Content = mainMenuView;
		}

		private void ShowSurfaceMining()
		{
			ContentArea.Content = surfaceMiningView;
		}

		private void ShowAsteroidMining()  
		{
			ContentArea.Content = asteroidMiningView;
		}

		public void PositionWindowToRight(Window window)
		{
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Left = this.Left + this.ActualWidth + 10;
			window.Top = this.Top;
		}
	}
}