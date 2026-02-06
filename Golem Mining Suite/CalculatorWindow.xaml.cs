using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite
{
    public partial class CalculatorWindow : Window
    {
        private Dictionary<string, double> defaultMineralPrices;
        private Dictionary<int, StationInfo> stations; // Changed to use terminal ID as key
        private Dictionary<int, Dictionary<string, double>> stationPrices; // terminal ID -> mineral -> price_sell
        private static readonly HttpClient httpClient = new HttpClient();
        private bool pricesLoaded = false;

        public CalculatorWindow()
        {
            InitializeComponent();
            LoadDefaultMineralPrices();
            _ = LoadStationsAsync(); // Fire and forget
        }

        private void LoadDefaultMineralPrices()
        {
            // Default prices per SCU in aUEC (fallback if API fails)
            // Note: "Quantanium" in our UI maps to "Quantainium" in the API
            defaultMineralPrices = new Dictionary<string, double>
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
        }

        // Map UI mineral names to API commodity names
        private string MapMineralToAPI(string mineralName)
        {
            // The API uses "Quantainium" instead of "Quantanium"
            if (mineralName == "Quantanium")
                return "Quantainium";
            return mineralName;
        }

        private async Task LoadStationsAsync()
        {
            try
            {
                stations = new Dictionary<int, StationInfo>();
                stationPrices = new Dictionary<int, Dictionary<string, double>>();

                // Load stations
                string stationsJson = LoadStationsFromFile();
                if (string.IsNullOrEmpty(stationsJson))
                {
                    stationsJson = await FetchStationsFromApiAsync();
                }

                if (!string.IsNullOrEmpty(stationsJson))
                {
                    ParseStationsJson(stationsJson);
                }

                // Load prices from API
                await LoadPricesFromApiAsync();

                // Populate UI
                Dispatcher.Invoke(() =>
                {
                    PopulateStationComboBox();
                    PopulateMineralComboBox();
                    UpdateStationInfo();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to load data: {ex.Message}\nUsing default prices.", 
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PopulateMineralComboBox();
                });
            }
        }

        private string LoadStationsFromFile()
        {
            try
            {
                // Try to load from a local terminals.json file in the application directory
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "terminals.json");
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
            }
            catch { }
            return null;
        }

        private async Task<string> FetchStationsFromApiAsync()
        {
            try
            {
                var response = await httpClient.GetAsync("https://uexcorp.space/api/terminals");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch { }
            return null;
        }

        private async Task LoadPricesFromApiAsync()
        {
            try
            {
                // First try to load from local file
                string pricesJson = LoadPricesFromFile();
                
                if (string.IsNullOrEmpty(pricesJson))
                {
                    // Fetch from API
                    System.Diagnostics.Debug.WriteLine("Fetching fresh price data from API...");
                    var response = await httpClient.GetAsync("https://uexcorp.space/api/commodities_prices_all");
                    System.Diagnostics.Debug.WriteLine($"API response status: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        pricesJson = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"API response length: {pricesJson?.Length ?? 0} characters");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"API request failed with status: {response.StatusCode}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Using cached price data from local file");
                }

                if (!string.IsNullOrEmpty(pricesJson))
                {
                    ParsePricesJson(pricesJson);
                    pricesLoaded = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load prices: {ex.Message}");
            }
        }

        private string LoadPricesFromFile()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commodities_prices_all.json");
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
            }
            catch { }
            return null;
        }

        private void ParsePricesJson(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var data = doc.RootElement.GetProperty("data");
                    
                    foreach (var priceEntry in data.EnumerateArray())
                    {
                        int terminalId = priceEntry.GetProperty("id_terminal").GetInt32();
                        string commodityName = priceEntry.GetProperty("commodity_name").GetString();
                        int priceSell = priceEntry.GetProperty("price_sell").GetInt32();

                        // Only store if station exists and price is valid
                        if (stations.ContainsKey(terminalId) && priceSell > 0)
                        {
                            if (!stationPrices.ContainsKey(terminalId))
                            {
                                stationPrices[terminalId] = new Dictionary<string, double>();
                            }
                            
                            stationPrices[terminalId][commodityName] = priceSell;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Loaded prices for {stationPrices.Count} stations");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse prices JSON: {ex.Message}");
            }
        }

        private void ParseStationsJson(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var data = doc.RootElement.GetProperty("data");
                    
                    foreach (var terminal in data.EnumerateArray())
                    {
                        if (terminal.GetProperty("type").GetString() == "commodity" &&
                            terminal.GetProperty("is_available").GetInt32() == 1)
                        {
                            var stationInfo = new StationInfo
                            {
                                Id = terminal.GetProperty("id").GetInt32(),
                                Code = terminal.GetProperty("code").GetString(),
                                DisplayName = terminal.GetProperty("displayname").GetString(),
                                StarSystem = terminal.GetProperty("star_system_name").GetString(),
                                Planet = GetStringOrNull(terminal, "planet_name")
                            };

                            stations[stationInfo.Id] = stationInfo;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse stations JSON: {ex.Message}");
            }
        }

        private string GetStringOrNull(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out JsonElement prop))
            {
                return prop.ValueKind == JsonValueKind.Null ? null : prop.GetString();
            }
            return null;
        }

        private void PopulateStationComboBox()
        {
            var stationList = new List<string> { "Default Prices" };
            
            if (stations != null && stations.Count > 0)
            {
                // Remove duplicates by using distinct display names
                var uniqueStations = stations.Values
                    .GroupBy(s => s.DisplayName)
                    .Select(g => g.First()) // Take first occurrence of each display name
                    .OrderBy(s => s.DisplayName)
                    .Select(s => $"{s.DisplayName} ({s.StarSystem})");
                
                stationList.AddRange(uniqueStations);
            }

            StationComboBox.ItemsSource = stationList;
            StationComboBox.SelectedIndex = 0;
        }

        private void PopulateMineralComboBox()
        {
            MineralComboBox.ItemsSource = defaultMineralPrices.Keys.OrderBy(x => x).ToList();
            if (MineralComboBox.Items.Count > 0)
                MineralComboBox.SelectedIndex = 0;
        }

        private void StationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateValue();
            UpdateStationInfo();
        }

        private void ComparePricesButton_Click(object sender, RoutedEventArgs e)
        {
            if (MineralComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a mineral first.", "No Mineral Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!pricesLoaded || stationPrices.Count == 0)
            {
                MessageBox.Show("Price data is still loading or unavailable. Please wait a moment and try again.", 
                    "Price Data Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedMineral = MineralComboBox.SelectedItem.ToString();
            
            var comparisonWindow = new PriceComparisonWindow(selectedMineral, stations, stationPrices);
            comparisonWindow.ShowDialog();
        }

        private void UpdateStationInfo()
        {
            if (StationComboBox.SelectedIndex == 0)
            {
                StationInfoText.Text = "Using default prices";
                return;
            }
            
            if (StationComboBox.SelectedItem == null)
                return;

            string selectedStation = StationComboBox.SelectedItem.ToString();
            
            // Find matching station
            StationInfo station = null;
            foreach (var s in stations.Values)
            {
                string stationDisplay = $"{s.DisplayName} ({s.StarSystem})";
                if (stationDisplay == selectedStation)
                {
                    station = s;
                    break;
                }
            }
            
            if (station != null && stationPrices.ContainsKey(station.Id))
            {
                int priceCount = stationPrices[station.Id].Count;
                StationInfoText.Text = $"Station: {station.DisplayName} ({priceCount} commodities with live prices)";
            }
            else if (station != null)
            {
                StationInfoText.Text = $"Station: {station.DisplayName} (using default prices - no live data)";
            }
            else
            {
                StationInfoText.Text = $"Station: {selectedStation}";
            }
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            CalculateValue();
        }

        private void MineralComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateValue();
            UpdateStationInfo(); // Update station info when mineral changes
        }

        private void ScuTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateValue();
        }

        private void CalculateValue()
        {
            if (MineralComboBox.SelectedItem == null)
            {
                ResultText.Text = "Total Value: 0 aUEC";
                DetailText.Text = "";
                return;
            }

            string selectedMineral = MineralComboBox.SelectedItem.ToString();

            // Allow empty input to show 0
            if (string.IsNullOrWhiteSpace(ScuTextBox.Text))
            {
                double pricePerScu = GetMineralPrice(selectedMineral);
                ResultText.Text = "Total Value: 0 aUEC";
                DetailText.Text = $"Price per SCU: {pricePerScu:N2} aUEC";
                return;
            }

            if (double.TryParse(ScuTextBox.Text, out double scuAmount))
            {
                double pricePerScu = GetMineralPrice(selectedMineral);
                double totalValue = scuAmount * pricePerScu;

                ResultText.Text = $"Total Value: {totalValue:N2} aUEC";
                DetailText.Text = $"{scuAmount} SCU × {pricePerScu:N2} aUEC/SCU";
            }
            else
            {
                ResultText.Text = "Invalid SCU amount";
                DetailText.Text = "Please enter a valid number";
            }
        }

        private double GetMineralPrice(string mineralName)
        {
            // If using default prices or no station selected
            if (StationComboBox.SelectedIndex == 0)
            {
                return defaultMineralPrices.ContainsKey(mineralName) 
                    ? defaultMineralPrices[mineralName] 
                    : 0;
            }

            // Get selected station
            string selectedStation = StationComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedStation))
            {
                return defaultMineralPrices.ContainsKey(mineralName) 
                    ? defaultMineralPrices[mineralName] 
                    : 0;
            }

            // Find station - check all stations for matching display name
            StationInfo station = null;
            foreach (var s in stations.Values)
            {
                string stationDisplay = $"{s.DisplayName} ({s.StarSystem})";
                if (stationDisplay == selectedStation)
                {
                    station = s;
                    break;
                }
            }

            if (station != null && stationPrices.ContainsKey(station.Id))
            {
                // Map UI mineral name to API commodity name
                string apiCommodityName = MapMineralToAPI(mineralName);
                
                var prices = stationPrices[station.Id];
                if (prices.ContainsKey(apiCommodityName))
                {
                    System.Diagnostics.Debug.WriteLine($"Found price for {apiCommodityName} at {station.DisplayName}: {prices[apiCommodityName]}");
                    return prices[apiCommodityName];
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No price for {apiCommodityName} at {station.DisplayName}, using default");
                }
            }
            else if (station != null)
            {
                System.Diagnostics.Debug.WriteLine($"Station {station.DisplayName} has no price data, using default");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Could not find station: {selectedStation}");
            }

            // Fallback to default prices
            return defaultMineralPrices.ContainsKey(mineralName) 
                ? defaultMineralPrices[mineralName] 
                : 0;
        }

    }

    public class StationInfo
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string DisplayName { get; set; }
        public string StarSystem { get; set; }
        public string Planet { get; set; }
    }
}
