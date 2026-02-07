using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Golem_Mining_Suite
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			// Set version from assembly
			var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

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

		private void SearchBox_KeyUp(object sender, KeyEventArgs e)
		{
			string searchText = SearchBox.Text;

			if (searchText == "Search mineral..." || string.IsNullOrWhiteSpace(searchText))
			{
				SuggestionsListBox.Visibility = Visibility.Collapsed;
				return;
			}

			var allData = GetMiningData();
			var suggestions = allData
				.Where(m => m.MineralName.ToLower().Contains(searchText.ToLower()))
				.Select(m => m.MineralName)
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
			if (SearchBox.Text == "Search mineral...")
			{
				SearchBox.Text = "";
				SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
			}
		}

		private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SearchBox.Text))
			{
				SearchBox.Text = "Search mineral...";
				SearchBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
			}

			Task.Delay(200).ContinueWith(_ =>
			{
				Dispatcher.Invoke(() => SuggestionsListBox.Visibility = Visibility.Collapsed);
			});
		}

		private void SuggestionsListBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (SuggestionsListBox.SelectedItem != null)
			{
				string selectedMineral = SuggestionsListBox.SelectedItem.ToString();

				var allData = GetMiningData();
				var foundMineral = allData.FirstOrDefault(m => m.MineralName.ToLower() == selectedMineral.ToLower());

				if (foundMineral != null)
				{
					var locationWindow = new LocationWindow(foundMineral.MineralName, true);
					PositionWindowToRight(locationWindow);
					locationWindow.Show();
				}

				SuggestionsListBox.Visibility = Visibility.Collapsed;
			}
		}

		private void MineralPrices_Click(object sender, RoutedEventArgs e)
		{
			var pricesWindow = new PricesWindow();
			PositionWindowToRight(pricesWindow);
			pricesWindow.Show();
		}

		private void CargoCalculator_Click(object sender, RoutedEventArgs e)
		{
			var calculatorWindow = new CalculatorWindow();
			PositionWindowToRight(calculatorWindow);
			calculatorWindow.Show();
		}

		private void OreDeposits_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("Use the search bar above to find specific minerals.", "Ore Deposits");
		}

		private void UexLinkButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

		private void PositionWindowToRight(Window window)
		{
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Left = this.Left + this.ActualWidth + 10;
			window.Top = this.Top;
		}

		private List<MineralData> GetMiningData()
		{
			return new List<MineralData>
			{
				new MineralData
				{
					MineralName = "Quantanium",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Granite", Percentage = 9 },
						new OreSource { OreName = "Shale", Percentage = 7 },
						new OreSource { OreName = "Igneous", Percentage = 7 },
						new OreSource { OreName = "Gneiss", Percentage = 4 },
						new OreSource { OreName = "Felsic", Percentage = 3 }
					}
				},
				new MineralData
				{
					MineralName = "Gold",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Granite", Percentage = 25 },
						new OreSource { OreName = "Shale", Percentage = 25 },
						new OreSource { OreName = "Atacamite", Percentage = 20 },
						new OreSource { OreName = "Igneous", Percentage = 19 }
					}
				},
				new MineralData
				{
					MineralName = "Bexalite",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Gneiss", Percentage = 18 },
						new OreSource { OreName = "Quartzite", Percentage = 13 },
						new OreSource { OreName = "Felsic", Percentage = 12 }
					}
				}
			};
		}
	}

	public class MineralData
	{
		public string MineralName { get; set; }
		public List<OreSource> OreSources { get; set; }
	}

	public class OreSource
	{
		public string OreName { get; set; }
		public double Percentage { get; set; }
	}
}