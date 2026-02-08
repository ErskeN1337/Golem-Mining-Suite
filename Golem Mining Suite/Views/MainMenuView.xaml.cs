using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite.Views
{
	public partial class MainMenuView : UserControl
	{
		public event EventHandler<string> NavigationRequested;

		public MainMenuView()
		{
			InitializeComponent();

			// Set version from assembly
			var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
		}

		private void SurfaceMining_Click(object sender, RoutedEventArgs e)
		{
			NavigationRequested?.Invoke(this, "SurfaceMining");
		}

		private void AsteroidMining_Click(object sender, RoutedEventArgs e)
		{
			NavigationRequested?.Invoke(this, "AsteroidMining");
		}

		private void RocMining_Click(object sender, RoutedEventArgs e)
		{
			NavigationRequested?.Invoke(this, "ROCMining");
		}

		private void UexLinkButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "https://uexcorp.space",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Could not open UEX Corp website: {ex.Message}",
					"Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}
	}
}