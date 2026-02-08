using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite
{
    public partial class CalculatorWindow : Window
    {
        private Dictionary<string, double> currentShipCapacity;
        private Dictionary<string, double> mineralPrices;
        private List<ComboBox> mineralComboBoxes;
        private List<TextBox> scuTextBoxes;

        public CalculatorWindow()
        {
            InitializeComponent();
            InitializeData();
            LoadShips();
            LoadStations();
            LoadMinerals();
        }

        private void InitializeData()
        {
            // Store references to controls for easier iteration
            mineralComboBoxes = new List<ComboBox>
            {
                Mineral1ComboBox, Mineral2ComboBox, Mineral3ComboBox,
                Mineral4ComboBox, Mineral5ComboBox
            };

            scuTextBoxes = new List<TextBox>
            {
                SCU1TextBox, SCU2TextBox, SCU3TextBox,
                SCU4TextBox, SCU5TextBox
            };

            // Ship cargo capacities (in SCU)
            currentShipCapacity = new Dictionary<string, double>
            {
                { "Prospector", 32 },
                { "MOLE", 96 },
                { "ROC", 1.2 },
                { "Cutlass Black", 46 },
                { "Freelancer MAX", 122 },
                { "Constellation Andromeda", 96 },
                { "Caterpillar", 576 },
                { "C2 Hercules", 696 }
            };

            // Default mineral prices (aUEC per SCU) - from UEX Corp
            mineralPrices = new Dictionary<string, double>
            {
                { "Quantanium", 88800 },
                { "Bexalite", 40100 },
                { "Taranite", 36000 },
                { "Borase", 32450 },
                { "Laranite", 30450 },
                { "Agricium", 25550 },
                { "Hephaestanite", 23600 },
                { "Gold", 6204 },
                { "Copper", 6030 },
                { "Beryl", 4930 },
                { "Tungsten", 3825 },
                { "Diamond", 7005 },
                { "Titanium", 8335 },
                { "Corundum", 2525 },
                { "Quartz", 1525 },
                { "Aluminum", 1230 },
                { "Iron", 855 }
            };
        }

        private void LoadShips()
        {
            ShipComboBox.ItemsSource = currentShipCapacity.Keys.OrderBy(s => s).ToList();
            ShipComboBox.SelectedIndex = 0; // Default to Prospector
        }

        private void LoadStations()
        {
            var stations = new List<string>
            {
                "Default Prices",
                "Port Tressler (+5%)",
                "Port Olisar (Standard)",
                "Everus Harbor (Standard)",
                "Baijini Point (Standard)",
                "Seraphim Station (Standard)"
            };

            StationComboBox.ItemsSource = stations;
            StationComboBox.SelectedIndex = 0;
        }

        private void LoadMinerals()
        {
            var minerals = new List<string> { "None" };
            minerals.AddRange(mineralPrices.Keys.OrderBy(m => m));

            foreach (var comboBox in mineralComboBoxes)
            {
                comboBox.ItemsSource = minerals;
                comboBox.SelectedIndex = 0;
            }
        }

        private void ShipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCargoCapacity();
        }

        private void StationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateTotalValue();
        }

        private void MineralComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateTotalValue();
        }

        private void SCUTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTotalValue();
        }

        private void UpdateCargoCapacity()
        {
            if (ShipComboBox.SelectedItem == null || scuTextBoxes == null)
                return;

            string selectedShip = ShipComboBox.SelectedItem.ToString();
            double maxCapacity = currentShipCapacity[selectedShip];
            double usedCapacity = 0;

            // Calculate total SCU used
            foreach (var textBox in scuTextBoxes)
            {
                if (double.TryParse(textBox.Text, out double scu))
                {
                    usedCapacity += scu;
                }
            }

            // Update progress bar and text
            double percentage = maxCapacity > 0 ? (usedCapacity / maxCapacity) * 100 : 0;
            CargoProgressBar.Value = Math.Min(percentage, 100);
            CargoCapacityText.Text = $"{usedCapacity:F1} / {maxCapacity:F1} SCU";

            // Change color if over capacity
            if (usedCapacity > maxCapacity)
            {
                CargoCapacityText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                CargoCapacityText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF8C42"));
            }
        }

        private void CalculateTotalValue()
        {
            if (mineralComboBoxes == null || scuTextBoxes == null)
                return;

            double totalValue = 0;
            double totalSCU = 0;
            string bestLocation = "Port Tressler";

            // Get station multiplier
            double stationMultiplier = 1.0;
            if (StationComboBox.SelectedItem != null)
            {
                string station = StationComboBox.SelectedItem.ToString();
                if (station.Contains("Port Tressler"))
                {
                    stationMultiplier = 1.05; // 5% bonus
                    bestLocation = "Port Tressler";
                }
            }

            // Calculate total value from all mineral rows
            for (int i = 0; i < mineralComboBoxes.Count; i++)
            {
                var mineralCombo = mineralComboBoxes[i];
                var scuTextBox = scuTextBoxes[i];

                if (mineralCombo.SelectedItem == null || mineralCombo.SelectedItem.ToString() == "None")
                    continue;

                string mineralName = mineralCombo.SelectedItem.ToString();
                if (!mineralPrices.ContainsKey(mineralName))
                    continue;

                if (double.TryParse(scuTextBox.Text, out double scu) && scu > 0)
                {
                    double price = mineralPrices[mineralName] * stationMultiplier;
                    totalValue += price * scu;
                    totalSCU += scu;
                }
            }

            // Update UI
            TotalValueText.Text = $"Total Value: {totalValue:N0} aUEC";
            BestLocationText.Text = $"💰 Best Price At: {bestLocation}";

            if (totalSCU > 0)
            {
                double avgPrice = totalValue / totalSCU;
                PricePerSCUText.Text = $"Average: {avgPrice:N0} aUEC/SCU";
            }
            else
            {
                PricePerSCUText.Text = "Average: 0 aUEC/SCU";
            }

            // Update cargo capacity
            UpdateCargoCapacity();
        }

        private void ClearRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                int rowIndex = int.Parse(button.Tag.ToString()) - 1;
                mineralComboBoxes[rowIndex].SelectedIndex = 0;
                scuTextBoxes[rowIndex].Text = "0";
            }
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            // Open PricesWindow for comparison
            var pricesWindow = new PricesWindow();
            pricesWindow.Show();
        }
    }
}
