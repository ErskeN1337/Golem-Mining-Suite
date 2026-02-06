using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite
{
    public partial class PriceComparisonWindow : Window
    {
        private string mineralName;
        private List<StationPrice> allPrices;

        public PriceComparisonWindow(string mineral, Dictionary<int, StationInfo> stations, 
                                     Dictionary<int, Dictionary<string, double>> stationPrices)
        {
            InitializeComponent();
            mineralName = mineral;
            LoadPrices(stations, stationPrices);
            UpdateDisplay();
        }

        private void LoadPrices(Dictionary<int, StationInfo> stations, 
                               Dictionary<int, Dictionary<string, double>> stationPrices)
        {
            allPrices = new List<StationPrice>();

            // Map UI mineral name to API name
            string apiMineralName = MapMineralToAPI(mineralName);

            foreach (var stationKvp in stations)
            {
                int stationId = stationKvp.Key;
                StationInfo station = stationKvp.Value;

                if (stationPrices.ContainsKey(stationId))
                {
                    var prices = stationPrices[stationId];
                    if (prices.ContainsKey(apiMineralName))
                    {
                        allPrices.Add(new StationPrice
                        {
                            StationName = station.DisplayName,
                            System = station.StarSystem,
                            Price = prices[apiMineralName]
                        });
                    }
                }
            }

            // Sort by highest price initially
            allPrices = allPrices.OrderByDescending(p => p.Price).ToList();

            // Add rank
            for (int i = 0; i < allPrices.Count; i++)
            {
                allPrices[i].Rank = i + 1;
            }
        }

        private string MapMineralToAPI(string mineralName)
        {
            if (mineralName == "Quantanium")
                return "Quantainium";
            return mineralName;
        }

        private void UpdateDisplay()
        {
            TitleText.Text = $"Price Comparison - {mineralName}";

            if (allPrices.Count == 0)
            {
                HighestPriceText.Text = "No data";
                AveragePriceText.Text = "No data";
                LowestPriceText.Text = "No data";
                return;
            }

            PriceListControl.ItemsSource = allPrices;

            double highest = allPrices.Max(p => p.Price);
            double lowest = allPrices.Min(p => p.Price);
            double average = allPrices.Average(p => p.Price);

            HighestPriceText.Text = $"{highest:N0} aUEC";
            AveragePriceText.Text = $"{average:N0} aUEC";
            LowestPriceText.Text = $"{lowest:N0} aUEC";
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (allPrices == null || allPrices.Count == 0)
                return;

            var selectedItem = (ComboBoxItem)SortComboBox.SelectedItem;
            string sortOption = selectedItem.Content.ToString();

            switch (sortOption)
            {
                case "Highest Price":
                    allPrices = allPrices.OrderByDescending(p => p.Price).ToList();
                    break;
                case "Lowest Price":
                    allPrices = allPrices.OrderBy(p => p.Price).ToList();
                    break;
                case "Station Name":
                    allPrices = allPrices.OrderBy(p => p.StationName).ToList();
                    break;
            }

            // Update rank
            for (int i = 0; i < allPrices.Count; i++)
            {
                allPrices[i].Rank = i + 1;
            }

            PriceListControl.ItemsSource = null;
            PriceListControl.ItemsSource = allPrices;
        }
    }

    public class StationPrice
    {
        public string StationName { get; set; }
        public string System { get; set; }
        public double Price { get; set; }
        public int Rank { get; set; }
    }
}
