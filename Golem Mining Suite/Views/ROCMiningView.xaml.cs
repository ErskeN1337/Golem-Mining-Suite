using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Golem_Mining_Suite.Data;

namespace Golem_Mining_Suite.Views
{
	public partial class ROCMiningView : UserControl
	{
		public event EventHandler BackToMenuRequested;

		public ROCMiningView()
		{
			InitializeComponent();

			// Set version from assembly
			var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
		}

		private void BackButton_Click(object sender, RoutedEventArgs e)
		{
			BackToMenuRequested?.Invoke(this, EventArgs.Empty);
		}

		private void SearchBox_KeyUp(object sender, KeyEventArgs e)
		{
			string searchText = SearchBox.Text;

			if (searchText == "Search rock type..." || string.IsNullOrWhiteSpace(searchText))
			{
				SuggestionsListBox.Visibility = Visibility.Collapsed;
				return;
			}

			var rockTypes = ROCMiningData.GetAllRockTypes();
			var suggestions = rockTypes
				.Where(r => r.ToLower().Contains(searchText.ToLower()))
				.ToList();

			if (suggestions.Count > 0)
			{
				SuggestionsListBox.ItemsSource = suggestions;
				SuggestionsListBox.Visibility = Visibility.Visible;
			}
			else
			{
				SuggestionsListBox.Visibility = Visibility.Collapsed;
			}
		}

		private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (SearchBox.Text == "Search rock type...")
			{
				SearchBox.Text = "";
				SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
			}
		}

		private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SearchBox.Text))
			{
				SearchBox.Text = "Search rock type...";
				SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
			}

			System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
			{
				Dispatcher.Invoke(() => SuggestionsListBox.Visibility = Visibility.Collapsed);
			});
		}

		private void SuggestionsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (SuggestionsListBox.SelectedItem != null)
			{
				string selectedRockType = SuggestionsListBox.SelectedItem.ToString();

				// Open LocationWindow for ROC mining (rocMode = true)
				LocationWindow locationWindow = new LocationWindow(selectedRockType, false, false, true);
				locationWindow.ShowDialog();

				SuggestionsListBox.Visibility = Visibility.Collapsed;
			}
		}

		private void RockTypeButton_Click(object sender, RoutedEventArgs e)
		{
			Button clickedButton = sender as Button;
			string rockType = clickedButton?.Content.ToString();

			// Open LocationWindow for ROC mining (rocMode = true)
			LocationWindow locationWindow = new LocationWindow(rockType, false, false, true);
			locationWindow.ShowDialog();
		}

		private void MineralPrices_Click(object sender, RoutedEventArgs e)
		{
			var pricesWindow = new PricesWindow();
			var mainWindow = Window.GetWindow(this) as MainWindow;
			if (mainWindow != null)
			{
				mainWindow.PositionWindowToRight(pricesWindow);
			}
			pricesWindow.Show();
		}

		private void CargoCalculator_Click(object sender, RoutedEventArgs e)
		{
			var calculatorWindow = new CalculatorWindow();
			var mainWindow = Window.GetWindow(this) as MainWindow;
			if (mainWindow != null)
			{
				mainWindow.PositionWindowToRight(calculatorWindow);
			}
			calculatorWindow.Show();
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