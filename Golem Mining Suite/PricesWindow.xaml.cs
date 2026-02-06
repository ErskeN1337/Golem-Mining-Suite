using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace Golem_Mining_Suite
{
	public partial class PricesWindow : Window
	{
		public PricesWindow()
		{
			InitializeComponent();
		}

		private async void Window_Loaded(object sender, RoutedEventArgs e)
		{
			StatusText.Text = "Loading prices from UEX Corp API...";
			var prices = await FetchPricesFromAPI();

			if (prices.Count == 0)
			{
				prices = GetFallbackPrices();
				StatusText.Text = "Failed to load prices - showing cached data";
			}
			else
			{
				StatusText.Text = $"Loaded {prices.Count} live mineral prices from API";
			}

			PricesGrid.ItemsSource = prices;
		}

		private async Task<List<PriceData>> FetchPricesFromAPI()
		{
			var priceList = new List<PriceData>();

			try
			{
				using (var client = new HttpClient())
				{
					client.Timeout = TimeSpan.FromSeconds(30);

					var response = await client.GetStringAsync("https://uexcorp.space/api/2.0/commodities");
					var jsonDoc = JsonDocument.Parse(response);
					var commodities = jsonDoc.RootElement.GetProperty("data");

					var mineralData = new Dictionary<string, double>();
					var bestLocations = GetBestLocations();

					foreach (var commodity in commodities.EnumerateArray())
					{
						var name = commodity.GetProperty("name").GetString();
						var baseName = name.Replace(" (Raw)", "").Replace(" (Ore)", "");

						if (IsMineralName(baseName))
						{
							double price = 0;

							if (commodity.TryGetProperty("price_sell", out var priceSell))
							{
								price = priceSell.GetDouble();
							}
							else if (commodity.TryGetProperty("price_buy", out var priceBuy))
							{
								price = priceBuy.GetDouble();
							}

							if (price > 0 && (!mineralData.ContainsKey(baseName) || price > mineralData[baseName]))
							{
								mineralData[baseName] = price;
							}
						}
					}

					foreach (var mineral in mineralData)
					{
						var displayName = mineral.Key == "Quantainium" ? "Quantanium" : mineral.Key;
						var location = bestLocations.ContainsKey(displayName) ? bestLocations[displayName] : "Unknown";

						priceList.Add(new PriceData
						{
							MineralName = displayName,
							Price = $"{mineral.Value:N0} aUEC",
							BestLocation = location
						});
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"API error: {ex.Message}");
			}

			return priceList.OrderByDescending(p => ParsePrice(p.Price)).ToList();
		}

		private Dictionary<string, string> GetBestLocations()
		{
			// These are general best locations - prices fluctuate but these are consistently good
			return new Dictionary<string, string>
			{
				{"Quantanium", "Area18 - ArcCorp"},
				{"Bexalite", "Area18 - ArcCorp"},
				{"Taranite", "Area18 - ArcCorp"},
				{"Laranite", "Lorville - Hurston"},
				{"Agricium", "Lorville - Hurston"},
				{"Hephaestanite", "Lorville - Hurston"},
				{"Beryl", "New Babbage - microTech"},
				{"Gold", "Lorville - Hurston"},
				{"Borase", "Lorville - Hurston"},
				{"Tungsten", "Lorville - Hurston"},
				{"Titanium", "Lorville - Hurston"},
				{"Corundum", "New Babbage - microTech"},
				{"Copper", "Lorville - Hurston"},
				{"Iron", "Lorville - Hurston"},
				{"Quartz", "New Babbage - microTech"},
				{"Aluminum", "Area18 - ArcCorp"}
			};
		}

		private bool IsMineralName(string name)
		{
			var minerals = new HashSet<string>
			{
				"Quantainium", "Bexalite", "Taranite", "Laranite", "Agricium",
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
			var bestLocations = GetBestLocations();
			return new List<PriceData>
			{
				new PriceData { MineralName = "Quantanium", Price = "87,859 aUEC", BestLocation = bestLocations["Quantanium"] },
				new PriceData { MineralName = "Bexalite", Price = "84,800 aUEC", BestLocation = bestLocations["Bexalite"] },
				new PriceData { MineralName = "Taranite", Price = "84,214 aUEC", BestLocation = bestLocations["Taranite"] },
				new PriceData { MineralName = "Laranite", Price = "21,563 aUEC", BestLocation = bestLocations["Laranite"] },
				new PriceData { MineralName = "Agricium", Price = "20,741 aUEC", BestLocation = bestLocations["Agricium"] },
				new PriceData { MineralName = "Hephaestanite", Price = "18,630 aUEC", BestLocation = bestLocations["Hephaestanite"] },
				new PriceData { MineralName = "Beryl", Price = "7,684 aUEC", BestLocation = bestLocations["Beryl"] },
				new PriceData { MineralName = "Gold", Price = "7,508 aUEC", BestLocation = bestLocations["Gold"] },
				new PriceData { MineralName = "Borase", Price = "6,402 aUEC", BestLocation = bestLocations["Borase"] },
				new PriceData { MineralName = "Tungsten", Price = "5,097 aUEC", BestLocation = bestLocations["Tungsten"] },
				new PriceData { MineralName = "Titanium", Price = "4,801 aUEC", BestLocation = bestLocations["Titanium"] },
				new PriceData { MineralName = "Corundum", Price = "4,030 aUEC", BestLocation = bestLocations["Corundum"] },
				new PriceData { MineralName = "Copper", Price = "4,008 aUEC", BestLocation = bestLocations["Copper"] },
				new PriceData { MineralName = "Iron", Price = "2,323 aUEC", BestLocation = bestLocations["Iron"] },
				new PriceData { MineralName = "Quartz", Price = "2,109 aUEC", BestLocation = bestLocations["Quartz"] },
				new PriceData { MineralName = "Aluminum", Price = "1,834 aUEC", BestLocation = bestLocations["Aluminum"] }
			};
		}
	}

	public class PriceData
	{
		public string MineralName { get; set; }
		public string Price { get; set; }
		public string BestLocation { get; set; }
	}
}