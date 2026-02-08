using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite
{
    public partial class RefineryCalculatorWindow : Window
    {
        private List<MineralRow> mineralRows = new List<MineralRow>();
        private Dictionary<string, double> mineralPrices;
        private Dictionary<string, RefineryMethod> refineryMethods;
        private Dictionary<string, Dictionary<string, double>> refineryYields;
        private const int MAX_MINERALS = 10;

        public RefineryCalculatorWindow()
        {
            InitializeComponent();
            InitializeData();
            AddMineralRow(); // Start with one row
        }

        private void InitializeData()
        {
            // Mineral prices (aUEC per SCU)
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

            refineryMethods = new Dictionary<string, RefineryMethod>();
            refineryYields = new Dictionary<string, Dictionary<string, double>>();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Loading refinery data from UEX Corp API...";
            
            await LoadRefineryMethods();
            await LoadRefineryLocations();
            
            if (refineryMethods.Count > 0)
            {
                StatusText.Text = $"Loaded {refineryMethods.Count} refinery methods";
            }
            else
            {
                StatusText.Text = "Using fallback refinery data";
                LoadFallbackRefineryData();
            }

            PopulateRefineryComboBox();
        }

        private async Task LoadRefineryMethods()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_methods");
                    var jsonDoc = JsonDocument.Parse(response);
                    var methods = jsonDoc.RootElement.GetProperty("data");

                    foreach (var method in methods.EnumerateArray())
                    {
                        string name = method.GetProperty("name").GetString();
                        string code = method.GetProperty("code").GetString();
                        int yieldRating = method.GetProperty("rating_yield").GetInt32();
                        int costRating = method.GetProperty("rating_cost").GetInt32();
                        int speedRating = method.GetProperty("rating_speed").GetInt32();

                        // Calculate percentages based on ratings
                        double yieldPercent = yieldRating == 3 ? 70 : (yieldRating == 2 ? 50 : 30);
                        double costPercent = costRating == 3 ? 15 : (costRating == 2 ? 10 : 7);

                        refineryMethods[name] = new RefineryMethod
                        {
                            Name = name,
                            Code = code,
                            YieldBonus = yieldPercent,
                            CostPercent = costPercent,
                            YieldRating = yieldRating,
                            CostRating = costRating,
                            SpeedRating = speedRating
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading refinery methods: {ex.Message}");
            }
        }

        private async Task LoadRefineryLocations()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_yields");
                    var jsonDoc = JsonDocument.Parse(response);
                    var yields = jsonDoc.RootElement.GetProperty("data");

                    foreach (var yieldData in yields.EnumerateArray())
                    {
                        string terminal = yieldData.GetProperty("terminal_name").GetString();
                        string commodity = yieldData.GetProperty("commodity_name").GetString();
                        int value = yieldData.GetProperty("value").GetInt32();

                        if (!refineryYields.ContainsKey(terminal))
                        {
                            refineryYields[terminal] = new Dictionary<string, double>();
                        }

                        refineryYields[terminal][commodity] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading refinery yields: {ex.Message}");
            }
        }

        private void LoadFallbackRefineryData()
        {
            // Fallback data if API fails
            refineryMethods["Dinyx Solvents"] = new RefineryMethod { Name = "Dinyx Solvents", YieldBonus = 70, CostPercent = 15 };
            refineryMethods["Cormack Method"] = new RefineryMethod { Name = "Cormack Method", YieldBonus = 50, CostPercent = 10 };
            refineryMethods["XCR Reaction"] = new RefineryMethod { Name = "XCR Reaction", YieldBonus = 30, CostPercent = 7 };
        }

        private void PopulateRefineryComboBox()
        {
            var locations = new List<string> { "Default Refinery" };
            
            if (refineryYields.Count > 0)
            {
                locations.AddRange(refineryYields.Keys.OrderBy(k => k));
            }

            RefineryComboBox.ItemsSource = locations;
            RefineryComboBox.SelectedIndex = 0;

            MethodComboBox.ItemsSource = refineryMethods.Keys.OrderBy(k => k).ToList();
            if (MethodComboBox.Items.Count > 0)
            {
                MethodComboBox.SelectedIndex = 0;
            }
        }

        private void AddMineralRow()
        {
            if (mineralRows.Count >= MAX_MINERALS)
            {
                MessageBox.Show($"Maximum of {MAX_MINERALS} minerals reached!", "Limit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = new MineralRow();
            mineralRows.Add(row);

            var border = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            border.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252525"));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			var mineralLabel = new TextBlock
			{
				Text = $"Mineral {mineralRows.Count}:",
				Margin = new Thickness(0, 0, 0, 5),
				Foreground = new System.Windows.Media.SolidColorBrush(
		(System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"))
			};
			Grid.SetRow(mineralLabel, 0);

            Grid.SetColumn(mineralLabel, 0);

			var scuLabel = new TextBlock
			{
				Text = "SCU Amount:",
				Margin = new Thickness(0, 0, 0, 5),
				Foreground = new System.Windows.Media.SolidColorBrush(
		 (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"))
			};

			Grid.SetRow(scuLabel, 0);
            Grid.SetColumn(scuLabel, 2);

            var mineralCombo = new ComboBox();
            var minerals = new List<string> { "None" };
            minerals.AddRange(mineralPrices.Keys.OrderBy(m => m));
            mineralCombo.ItemsSource = minerals;
            mineralCombo.SelectedIndex = 0;
            mineralCombo.SelectionChanged += MineralComboBox_SelectionChanged;
            Grid.SetRow(mineralCombo, 2);
            Grid.SetColumn(mineralCombo, 0);
            row.MineralComboBox = mineralCombo;

            var scuTextBox = new TextBox { Text = "0" };
            scuTextBox.TextChanged += SCUTextBox_TextChanged;
            Grid.SetRow(scuTextBox, 2);
            Grid.SetColumn(scuTextBox, 2);
            row.SCUTextBox = scuTextBox;

            var removeButton = new Button
            {
                Content = "✖",
                Padding = new Thickness(8),
                Style = (Style)FindResource("ActionButton"),
                Tag = row
            };
            removeButton.Click += RemoveMineralRow_Click;
            Grid.SetRow(removeButton, 2);
            Grid.SetColumn(removeButton, 4);

            grid.Children.Add(mineralLabel);
            grid.Children.Add(scuLabel);
            grid.Children.Add(mineralCombo);
            grid.Children.Add(scuTextBox);
            grid.Children.Add(removeButton);

            border.Child = grid;
            MineralInputPanel.Children.Add(border);

            UpdateAddButtonVisibility();
        }

        private void RemoveMineralRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is MineralRow row)
            {
                int index = mineralRows.IndexOf(row);
                mineralRows.RemoveAt(index);
                MineralInputPanel.Children.RemoveAt(index);
                
                // Renumber remaining rows
                for (int i = 0; i < mineralRows.Count; i++)
                {
                    var border = MineralInputPanel.Children[i] as Border;
                    var grid = border.Child as Grid;
                    var label = grid.Children[0] as TextBlock;
                    label.Text = $"Mineral {i + 1}:";
                }

                UpdateAddButtonVisibility();
                CalculateProfit();
            }
        }

        private void AddMineralButton_Click(object sender, RoutedEventArgs e)
        {
            AddMineralRow();
        }

        private void UpdateAddButtonVisibility()
        {
            AddMineralButton.IsEnabled = mineralRows.Count < MAX_MINERALS;
        }

        private void RefineryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateProfit();
        }

        private void MethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateProfit();
        }

        private void MineralComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CalculateProfit();
        }

        private void SCUTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateProfit();
        }

        private void CalculateProfit()
        {
            if (MethodComboBox.SelectedItem == null) return;

            double totalRawValue = 0;
            double totalSCU = 0;

            // Calculate raw material value
            foreach (var row in mineralRows)
            {
                if (row.MineralComboBox.SelectedItem == null || 
                    row.MineralComboBox.SelectedItem.ToString() == "None")
                    continue;

                string mineral = row.MineralComboBox.SelectedItem.ToString();
                if (!mineralPrices.ContainsKey(mineral))
                    continue;

                if (double.TryParse(row.SCUTextBox.Text, out double scu) && scu > 0)
                {
                    double price = mineralPrices[mineral];
                    totalRawValue += price * scu;
                    totalSCU += scu;
                }
            }

            // Get refinery method
            string methodName = MethodComboBox.SelectedItem.ToString();
            RefineryMethod method = refineryMethods[methodName];

            // Calculate refinery costs and yields
            double refineryCost = totalRawValue * (method.CostPercent / 100.0);
            double yieldBonus = method.YieldBonus;
            
            // Calculate refined value with yield bonus
            double refinedValue = totalRawValue * (1 + yieldBonus / 100.0);
            double netProfit = refinedValue - refineryCost;

            // Update UI
            RawValueText.Text = $"Raw Material Value: {totalRawValue:N0} aUEC";
            RefineryCostText.Text = $"Refinery Fee ({method.CostPercent}%): -{refineryCost:N0} aUEC";
            YieldBonusText.Text = $"Yield Bonus: +{yieldBonus:F0}%";
            RefinedValueText.Text = $"Refined Material Value: {refinedValue:N0} aUEC";
            NetProfitText.Text = $"Net Profit: {netProfit:N0} aUEC";
        }

        private class MineralRow
        {
            public ComboBox MineralComboBox { get; set; }
            public TextBox SCUTextBox { get; set; }
        }

        private class RefineryMethod
        {
            public string Name { get; set; }
            public string Code { get; set; }
            public double YieldBonus { get; set; }
            public double CostPercent { get; set; }
            public int YieldRating { get; set; }
            public int CostRating { get; set; }
            public int SpeedRating { get; set; }
        }
    }
}
