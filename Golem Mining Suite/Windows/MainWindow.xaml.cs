using System;
using System.Threading.Tasks;
using System.Windows;
using Golem_Mining_Suite.ViewModels;
using Golem_Mining_Suite.Views;

namespace Golem_Mining_Suite
{
	public partial class MainWindow : Window
	{
		public MainWindow(MainViewModel viewModel)
		{
			InitializeComponent();
			DataContext = viewModel;

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