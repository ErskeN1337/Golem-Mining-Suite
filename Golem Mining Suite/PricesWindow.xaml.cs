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
		private static readonly HttpClient httpClient = new HttpClient();

		public PricesWindow()
		{
			InitializeComponent();
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			StatusText.Text = "Loading live prices from UEX Corp API...";
			var prices = await FetchPricesFromAPI();

			if (prices.Count == 0)
			{
				prices = GetFallbackPrices();
				StatusText.Text = "Failed to load prices - showing cached data";
			}
			else
			{
				StatusText.Text = $"Loaded {prices.Count} live mineral prices (highest selling price per mineral)";
			}

			PricesGrid.ItemsSource = prices;
		}

		private async Task<List<PriceData>> FetchPricesFromAPI()
		{
			var priceList = new List<PriceData>();

			try
			{
				// Fetch all commodity prices from all stations
				var response = await httpClient.GetAsync("https://uexcorp.space/api/commodities_prices_all");
				
				if (!response.IsSuccessStatusCode)
				{
					System.Diagnostics.Debug.WriteLine("Failed to fetch commodity prices");
					return priceList;
				}

				var json = await response.Content.ReadAsStringAsync();
				var jsonDoc = JsonDocument.Parse(json);
				var pricesData = jsonDoc.RootElement.GetProperty("data");

				// Dictionary to store highest price per mineral across all stations
				var mineralBestPrices = new Dictionary<string, MineralPriceInfo>();

				foreach (var priceEntry in pricesData.EnumerateArray())
				{
					var commodityName = priceEntry.GetProperty("commodity_name").GetString();
					var terminalName = priceEntry.GetProperty("terminal_name").GetString();
					var priceSell = priceEntry.GetProperty("price_sell").GetInt32();

					if (priceSell <= 0)
						continue;

					// Map API names to display names
					var displayName = MapCommodityName(commodityName);
					
					if (IsMineralName(displayName))
					{
						if (!mineralBestPrices.ContainsKey(displayName) || priceSell > mineralBestPrices[displayName].Price)
						{
							mineralBestPrices[displayName] = new MineralPriceInfo
							{
								MineralName = displayName,
								Price = priceSell,
								BestLocation = terminalName
							};
						}
					}
				}

				// Convert to display format
				foreach (var mineral in mineralBestPrices.Values)
				{
					priceList.Add(new PriceData
					{
						MineralName = mineral.MineralName,
						Price = $"{mineral.Price:N0} aUEC",
						BestLocation = mineral.BestLocation
					});
				}

				System.Diagnostics.Debug.WriteLine($"Loaded {priceList.Count} mineral prices from API");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
			}

			return priceList.OrderByDescending(p => ParsePrice(p.Price)).ToList();
		}

		private string MapCommodityName(string apiName)
		{
			// Map API names to display names (handles "Quantainium" -> "Quantanium")
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
				new PriceData { MineralName = "Quantanium", Price = "87,859 aUEC", BestLocation = "Area 18" },
				new PriceData { MineralName = "Bexalite", Price = "84,800 aUEC", BestLocation = "Area 18" },
				new PriceData { MineralName = "Taranite", Price = "84,214 aUEC", BestLocation = "Area 18" },
				new PriceData { MineralName = "Laranite", Price = "21,563 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Agricium", Price = "20,741 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Hephaestanite", Price = "18,630 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Beryl", Price = "7,684 aUEC", BestLocation = "New Babbage" },
				new PriceData { MineralName = "Gold", Price = "7,508 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Borase", Price = "6,402 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Tungsten", Price = "5,097 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Titanium", Price = "4,801 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Corundum", Price = "4,030 aUEC", BestLocation = "New Babbage" },
				new PriceData { MineralName = "Copper", Price = "4,008 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Iron", Price = "2,323 aUEC", BestLocation = "Lorville" },
				new PriceData { MineralName = "Quartz", Price = "2,109 aUEC", BestLocation = "New Babbage" },
				new PriceData { MineralName = "Aluminum", Price = "1,834 aUEC", BestLocation = "Area 18" }
			};
		}
	}

	public class PriceData
	{
		public string MineralName { get; set; }
		public string Price { get; set; }
		public string BestLocation { get; set; }
	}

	internal class MineralPriceInfo
	{
		public string MineralName { get; set; }
		public int Price { get; set; }
		public string BestLocation { get; set; }
	}
}
