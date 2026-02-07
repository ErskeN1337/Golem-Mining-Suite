using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Golem_Mining_Suite
{
	public partial class PricesWindow : Window
	{
		private List<PriceData> allPrices = new List<PriceData>();
		private Dictionary<int, string> terminalToSystem = new Dictionary<int, string>();

		public PricesWindow()
		{
			InitializeComponent();
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			StatusText.Text = "Loading terminals and prices from UEX Corp API...";

			terminalToSystem = await LoadTerminalSystemMapping();
			allPrices = await FetchPricesFromAPI();

			if (allPrices.Count == 0)
			{
				allPrices = GetFallbackPrices();
				StatusText.Text = "Failed to load prices - showing cached data";
			}
			else
			{
				StatusText.Text = $"Loaded {allPrices.Count} live mineral prices from API";
			}

			PopulateMineralFilter();
			ApplyFilter();
		}

		private async Task<Dictionary<int, string>> LoadTerminalSystemMapping()
		{
			var mapping = new Dictionary<int, string>();

			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(30);
					var response = await client.GetStringAsync("https://uexcorp.space/api/terminals");
					var jsonDoc = JsonDocument.Parse(response);
					var terminals = jsonDoc.RootElement.GetProperty("data");

					foreach (var terminal in terminals.EnumerateArray())
					{
						int id = terminal.GetProperty("id").GetInt32();
						string starSystem = terminal.GetProperty("star_system_name").GetString();
						mapping[id] = starSystem;
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to load terminals: {ex.Message}");
			}

			return mapping;
		}

		private void PopulateMineralFilter()
		{
			var minerals = allPrices.Select(p => p.MineralName).Distinct().OrderBy(m => m).ToList();

			MineralFilterComboBox.Items.Clear();
			MineralFilterComboBox.Items.Add("All Minerals");

			foreach (var mineral in minerals)
			{
				MineralFilterComboBox.Items.Add(mineral);
			}

			MineralFilterComboBox.SelectedIndex = 0;
		}

		private void FilterChanged(object sender, RoutedEventArgs e)
		{
			ApplyFilter();
		}

		private void ApplyFilter()
		{
			if (allPrices == null || allPrices.Count == 0)
				return;

			IEnumerable<PriceData> filtered = allPrices;

			if (StantonRadio?.IsChecked == true)
			{
				filtered = filtered.Where(p => p.StarSystem != null && p.StarSystem.Contains("Stanton"));
			}
			else if (PyroRadio?.IsChecked == true)
			{
				filtered = filtered.Where(p => p.StarSystem != null && p.StarSystem.Contains("Pyro"));
			}

			if (MineralFilterComboBox?.SelectedItem != null)
			{
				string selectedMineral = MineralFilterComboBox.SelectedItem.ToString();
				if (selectedMineral != "All Minerals")
				{
					filtered = filtered.Where(p => p.MineralName == selectedMineral);
				}
			}

			var sortedFiltered = filtered.OrderByDescending(p => ParsePrice(p.Price)).ToList();
			PricesGrid.ItemsSource = sortedFiltered;
			StatusText.Text = $"Showing {sortedFiltered.Count} results";
		}

		private async Task<List<PriceData>> FetchPricesFromAPI()
		{
			var priceList = new List<PriceData>();

			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(30);

					var response = await client.GetStringAsync("https://uexcorp.space/api/commodities_prices_all");
					var jsonDoc = JsonDocument.Parse(response);
					var pricesData = jsonDoc.RootElement.GetProperty("data");

					foreach (var priceEntry in pricesData.EnumerateArray())
					{
						var commodityName = priceEntry.GetProperty("commodity_name").GetString();
						var terminalName = priceEntry.GetProperty("terminal_name").GetString();
						var priceSell = priceEntry.GetProperty("price_sell").GetInt32();

						int terminalId = priceEntry.GetProperty("id_terminal").GetInt32();
						string starSystem = terminalToSystem.ContainsKey(terminalId) ? terminalToSystem[terminalId] : "Unknown";

						int scu = 0;
						int scuMax = 100;

						if (priceEntry.TryGetProperty("scu", out JsonElement scuElement))
						{
							scu = scuElement.GetInt32();
						}

						if (priceEntry.TryGetProperty("scu_max", out JsonElement scuMaxElement))
						{
							scuMax = scuMaxElement.GetInt32();
						}

						if (priceSell <= 0)
							continue;

						var displayName = MapCommodityName(commodityName);

						if (IsMineralName(displayName))
						{
							double inventoryPercent = scuMax > 0 ? (double)scu / scuMax * 100 : 0;
							string demand = inventoryPercent < 50 ? "High" : "Low";

							priceList.Add(new PriceData
							{
								MineralName = displayName,
								Price = $"{priceSell:N0} aUEC",
								BestLocation = terminalName,
								Demand = demand,
								StarSystem = starSystem
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"API Error: {ex.Message}");
			}

			return priceList;
		}

		private string MapCommodityName(string apiName)
		{
			if (apiName == "Quantainium")
				return "Quantanium";

			return apiName;
		}

		private bool IsMineralName(string name)
		{
			var minerals = new HashSet<string>
			{
				"Quantanium", "Bexalite", "Taranite", "Laranite", "Agricium",
				"Hephaestanite", "Beryl", "Gold", "Borase", "Tungsten",
				"Titanium", "Iron", "Quartz", "Copper", "Corundum", "Aluminum"
			};

			return minerals.Contains(name);
		}

		private double ParsePrice(string priceString)
		{
			var numericString = priceString.Replace(",", "").Replace(" aUEC", "");
			double.TryParse(numericString, out double result);
			return result;
		}

		private List<PriceData> GetFallbackPrices()
		{
			return new List<PriceData>
			{
				new PriceData { MineralName = "Quantanium", Price = "87,859 aUEC", BestLocation = "Area 18", Demand = "High", StarSystem = "Stanton" },
				new PriceData { MineralName = "Bexalite", Price = "84,800 aUEC", BestLocation = "Area 18", Demand = "High", StarSystem = "Stanton" }
			};
		}

		public class PriceData
		{
			public string MineralName { get; set; }
			public string Price { get; set; }
			public string BestLocation { get; set; }
			public string Demand { get; set; }
			public string StarSystem { get; set; }
		}

		internal class MineralPriceInfo
		{
			public string MineralName { get; set; }
			public int Price { get; set; }
			public string BestLocation { get; set; }
			public string Demand { get; set; }
			public string StarSystem { get; set; }
		}
	}
}