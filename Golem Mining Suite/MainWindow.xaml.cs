using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HtmlAgilityPack;
using System.Net.Http;

namespace Golem_Mining_Suite
{
	public partial class MainWindow : Window
	{
		private Dictionary<string, double> mineralPrices = new Dictionary<string, double>();
		public MainWindow()
		{
			InitializeComponent();
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			mineralPrices = await FetchAndParsePrices();
			
			// Set version text from assembly
			var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
			VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
		}

		private void SearchBox_KeyUp(object sender, KeyEventArgs e)
		{
			string searchText = SearchBox.Text;

			// Skip if it's the placeholder text or empty
			if (searchText == "Enter mineral name..." || string.IsNullOrWhiteSpace(searchText))
			{
				ResultsGrid.ItemsSource = null;
				SuggestionsListBox.Visibility = Visibility.Collapsed;
				return;
			}

			// Get all mining data
			var allData = GetMiningData();

			// Show suggestions
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

			// If there's an exact match, show results
			var foundMineral = allData.FirstOrDefault(m => m.MineralName.ToLower() == searchText.ToLower());

			if (foundMineral != null)
			{
				// Convert to OreResult for display
				var results = foundMineral.OreSources.Select(ore => new OreResult
				{
					OreDeposit = ore.OreName,
					Percentage = ore.Percentage + "%"
				}).ToList();

				ResultsGrid.ItemsSource = results;
				SuggestionsListBox.Visibility = Visibility.Collapsed;
			}
			else
			{
				// Clear results if nothing found
				ResultsGrid.ItemsSource = null;
			}
		}

		private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
		{
			if (SearchBox.Text == "Enter mineral name...")
			{
				SearchBox.Text = "";
			}
		}

		private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(SearchBox.Text))
			{
				SearchBox.Text = "Enter mineral name...";
			}

			// Hide suggestions when losing focus (with a small delay to allow clicking)
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
				SearchBox.Text = selectedMineral;
				SuggestionsListBox.Visibility = Visibility.Collapsed;

				// Trigger search for the selected mineral
				var allData = GetMiningData();
				var foundMineral = allData.FirstOrDefault(m => m.MineralName.ToLower() == selectedMineral.ToLower());

				if (foundMineral != null)
				{
					var results = foundMineral.OreSources.Select(ore => new OreResult
					{
						OreDeposit = ore.OreName,
						Percentage = ore.Percentage + "%"
					}).ToList();

					ResultsGrid.ItemsSource = results;
				}
			}
		}

		private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (ResultsGrid.SelectedItem is OreResult selectedOre)
			{
				// Show locations in the Locations tab
				LocationTitleText.Text = $"Best Locations for {selectedOre.OreDeposit}";
				LoadLocationData(selectedOre.OreDeposit);

				// Update tab header to include deposit name
				LocationsTabText.Text = $"Locations {selectedOre.OreDeposit}";

				// Show and switch to Locations tab
				LocationsTab.Visibility = Visibility.Visible;
				MainTabControl.SelectedItem = LocationsTab;
			}
		}

		private void LoadLocationData(string depositName)
		{
			var allLocationData = new Dictionary<string, Dictionary<string, int>>
			{
				{"Atacamite", new Dictionary<string, int> {
					{"Yela", 25}, {"Cellin", 9}, {"Aberdeen", 13}, {"Hurston", 13},
					{"Magda", 14}, {"Euterpe", 14}, {"Lyria", 16}, {"Daymar", 17},
					{"Arial", 11}, {"Ita", 17}, {"Microtech", 8}, {"Calliope", 15}, {"Wala", 7}
				}},
				{"Felsic", new Dictionary<string, int> {
					{"Yela", 16}, {"Cellin", 13}, {"Aberdeen", 7}, {"Microtech", 24},
					{"Arial", 19}, {"Ita", 9}, {"Magda", 5}, {"Euterpe", 5},
					{"Lyria", 19}, {"Wala", 4}, {"Clio", 23}, {"Daymar", 12}, {"Hurston", 11}
				}},
				{"Gneiss", new Dictionary<string, int> {
					{"Yela", 28}, {"Cellin", 27}, {"Aberdeen", 20}, {"Microtech", 21},
					{"Arial", 11}, {"Ita", 5}, {"Magda", 12}, {"Euterpe", 26},
					{"Lyria", 27}, {"Wala", 9}, {"Clio", 22}, {"Hurston", 11}
				}},
				{"Granite", new Dictionary<string, int> {
					{"Cellin", 23}, {"Aberdeen", 12}, {"Microtech", 16}, {"Arial", 16},
					{"Ita", 10}, {"Magda", 19}, {"Euterpe", 16}, {"Lyria", 12},
					{"Wala", 18}, {"Clio", 13}, {"Calliope", 15}, {"Hurston", 11}
				}},
				{"Igneous", new Dictionary<string, int> {
					{"Yela", 4}, {"Aberdeen", 8}, {"Hurston", 45}, {"Microtech", 14},
					{"Arial", 9}, {"Ita", 16}, {"Magda", 11}, {"Euterpe", 12},
					{"Lyria", 9}, {"Wala", 25}, {"Daymar", 18}, {"Calliope", 7}
				}},
				{"Obsidian", new Dictionary<string, int> {
					{"Yela", 5}, {"Cellin", 9}, {"Aberdeen", 7}, {"Microtech", 6},
					{"Arial", 8}, {"Ita", 18}, {"Magda", 9}, {"Euterpe", 12},
					{"Lyria", 5}, {"Wala", 5}, {"Clio", 9}, {"Calliope", 10}
				}},
				{"Quartzite", new Dictionary<string, int> {
					{"Aberdeen", 25}, {"Arial", 15}, {"Ita", 16},
					{"Magda", 8}, {"Euterpe", 3}, {"Calliope", 25}, {"Wala", 18}
				}},
				{"Shale", new Dictionary<string, int> {
					{"Yela", 13}, {"Cellin", 8}, {"Aberdeen", 8}, {"Hurston", 21},
					{"Microtech", 10}, {"Arial", 12}, {"Ita", 9}, {"Magda", 21},
					{"Euterpe", 13}, {"Lyria", 12}, {"Wala", 12}, {"Clio", 15},
					{"Calliope", 28}, {"Daymar", 20}
				}}
			};

			if (allLocationData.ContainsKey(depositName))
			{
				var locationResults = allLocationData[depositName]
					.OrderByDescending(x => x.Value)
					.Select(x => new LocationResult
					{
						LocationName = x.Key,
						Chance = x.Value + "%"
					})
					.ToList();

				LocationsGrid.ItemsSource = locationResults;
			}
		}
		
		private void CloseLocationsTab_Click(object sender, RoutedEventArgs e)
		{
			LocationsTab.Visibility = Visibility.Collapsed;
			MainTabControl.SelectedItem = DepositsTab;
		}

		private void MineralPrices_Click(object sender, RoutedEventArgs e)
		{
			var pricesWindow = new PricesWindow();
			pricesWindow.Show();
		}

		private void CargoCalculator_Click(object sender, RoutedEventArgs e)
		{
			var calculatorWindow = new CalculatorWindow();
			calculatorWindow.Show();
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

		private async Task<Dictionary<string, double>> FetchAndParsePrices()
		{
			var prices = new Dictionary<string, double>
	{
		{ "Quantanium", 87859 },
		{ "Bexalite", 84800 },
		{ "Taranite", 84214 },
		{ "Laranite", 21563 },
		{ "Agricium", 20741 },
		{ "Hephaestanite", 18630 },
		{ "Beryl", 7684 },
		{ "Gold", 7508 },
		{ "Borase", 6402 },
		{ "Tungsten", 5097 },
		{ "Titanium", 4801 },
		{ "Corundum", 4030 },
		{ "Copper", 4008 },
		{ "Iron", 2323 },
		{ "Quartz", 2109 },
		{ "Aluminum", 1834 }
	};

			return prices;
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
						new OreSource { OreName = "Felsic", Percentage = 3 },
						new OreSource { OreName = "Obsidian", Percentage = 3 },
						new OreSource { OreName = "Atacamite", Percentage = 3 },
						new OreSource { OreName = "Quartzite", Percentage = 2 }
					}
				},
				new MineralData
				{
					MineralName = "Bexalite",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Gneiss", Percentage = 18 },
						new OreSource { OreName = "Quartzite", Percentage = 13 },
						new OreSource { OreName = "Felsic", Percentage = 12 },
						new OreSource { OreName = "Atacamite", Percentage = 11 },
						new OreSource { OreName = "Granite", Percentage = 9 },
						new OreSource { OreName = "Igneous", Percentage = 9 },
						new OreSource { OreName = "Shale", Percentage = 7 },
						new OreSource { OreName = "Obsidian", Percentage = 7 }
					}
				},
				new MineralData
				{
					MineralName = "Taranite",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Felsic", Percentage = 20 },
						new OreSource { OreName = "Gneiss", Percentage = 19 },
						new OreSource { OreName = "Igneous", Percentage = 16 },
						new OreSource { OreName = "Quartzite", Percentage = 16 },
						new OreSource { OreName = "Obsidian", Percentage = 14 },
						new OreSource { OreName = "Shale", Percentage = 13 },
						new OreSource { OreName = "Granite", Percentage = 7 },
						new OreSource { OreName = "Atacamite", Percentage = 5 }
					}
				},
				new MineralData
				{
					MineralName = "Laranite",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Igneous", Percentage = 41 },
						new OreSource { OreName = "Shale", Percentage = 37 },
						new OreSource { OreName = "Granite", Percentage = 25 },
						new OreSource { OreName = "Atacamite", Percentage = 25 }
					}
				},
				new MineralData
				{
					MineralName = "Agricium",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Atacamite", Percentage = 26 },
						new OreSource { OreName = "Quartzite", Percentage = 16 }
					}
				},
				new MineralData
				{
					MineralName = "Hephaestanite",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Quartzite", Percentage = 40 },
						new OreSource { OreName = "Gneiss", Percentage = 39 },
						new OreSource { OreName = "Atacamite", Percentage = 28 }
					}
				},
				new MineralData
				{
					MineralName = "Beryl",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Felsic", Percentage = 49 },
						new OreSource { OreName = "Obsidian", Percentage = 36 }
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
						new OreSource { OreName = "Igneous", Percentage = 19 },
						new OreSource { OreName = "Obsidian", Percentage = 15 },
						new OreSource { OreName = "Gneiss", Percentage = 11 },
						new OreSource { OreName = "Felsic", Percentage = 9 },
						new OreSource { OreName = "Quartzite", Percentage = 6 }
					}
				},
				new MineralData
				{
					MineralName = "Borase",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Granite", Percentage = 44 },
						new OreSource { OreName = "Obsidian", Percentage = 18 }
					}
				},
				new MineralData
				{
					MineralName = "Tungsten",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Igneous", Percentage = 12 },
						new OreSource { OreName = "Felsic", Percentage = 8 },
						new OreSource { OreName = "Atacamite", Percentage = 8 },
						new OreSource { OreName = "Obsidian", Percentage = 6 }
					}
				},
				new MineralData
				{
					MineralName = "Titanium",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Gneiss", Percentage = 34 },
						new OreSource { OreName = "Felsic", Percentage = 26 },
						new OreSource { OreName = "Granite", Percentage = 25 },
						new OreSource { OreName = "Igneous", Percentage = 24 },
						new OreSource { OreName = "Atacamite", Percentage = 14 },
						new OreSource { OreName = "Shale", Percentage = 10 },
						new OreSource { OreName = "Quartzite", Percentage = 9 },
						new OreSource { OreName = "Obsidian", Percentage = 6 }
					}
				},
				new MineralData
				{
					MineralName = "Iron",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Quartzite", Percentage = 23 },
						new OreSource { OreName = "Obsidian", Percentage = 19 },
						new OreSource { OreName = "Shale", Percentage = 17 },
						new OreSource { OreName = "Igneous", Percentage = 14 },
						new OreSource { OreName = "Atacamite", Percentage = 13 },
						new OreSource { OreName = "Granite", Percentage = 10 },
						new OreSource { OreName = "Gneiss", Percentage = 9 }
					}
				},
				new MineralData
				{
					MineralName = "Quartz",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Felsic", Percentage = 22 },
						new OreSource { OreName = "Shale", Percentage = 14 },
						new OreSource { OreName = "Gneiss", Percentage = 14 },
						new OreSource { OreName = "Obsidian", Percentage = 14 },
						new OreSource { OreName = "Atacamite", Percentage = 13 },
						new OreSource { OreName = "Granite", Percentage = 11 },
						new OreSource { OreName = "Igneous", Percentage = 11 },
						new OreSource { OreName = "Quartzite", Percentage = 11 }
					}
				},
				new MineralData
				{
					MineralName = "Copper",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Shale", Percentage = 6 },
						new OreSource { OreName = "Gneiss", Percentage = 6 },
						new OreSource { OreName = "Felsic", Percentage = 5 },
						new OreSource { OreName = "Obsidian", Percentage = 5 },
						new OreSource { OreName = "Atacamite", Percentage = 4 },
						new OreSource { OreName = "Quartzite", Percentage = 4 },
						new OreSource { OreName = "Granite", Percentage = 3 },
						new OreSource { OreName = "Igneous", Percentage = 1 }
					}
				},
				new MineralData
				{
					MineralName = "Corundum",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Shale", Percentage = 22 },
						new OreSource { OreName = "Atacamite", Percentage = 18 },
						new OreSource { OreName = "Obsidian", Percentage = 17 },
						new OreSource { OreName = "Felsic", Percentage = 15 },
						new OreSource { OreName = "Quartzite", Percentage = 13 },
						new OreSource { OreName = "Gneiss", Percentage = 12 },
						new OreSource { OreName = "Igneous", Percentage = 10 },
						new OreSource { OreName = "Granite", Percentage = 10 }
					}
				},
				new MineralData
				{
					MineralName = "Aluminum",
					OreSources = new List<OreSource>
					{
						new OreSource { OreName = "Granite", Percentage = 42 },
						new OreSource { OreName = "Quartzite", Percentage = 41 },
						new OreSource { OreName = "Obsidian", Percentage = 36 },
						new OreSource { OreName = "Shale", Percentage = 32 },
						new OreSource { OreName = "Igneous", Percentage = 32 },
						new OreSource { OreName = "Gneiss", Percentage = 24 },
						new OreSource { OreName = "Atacamite", Percentage = 13 },
						new OreSource { OreName = "Felsic", Percentage = 13 }
					}
				}
			};
		}
	}

	public class OreResult
	{
		public string OreDeposit { get; set; }
		public string Percentage { get; set; }
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

	public class LocationResult
	{
		public string LocationName { get; set; }
		public string Chance { get; set; }
	}
}
