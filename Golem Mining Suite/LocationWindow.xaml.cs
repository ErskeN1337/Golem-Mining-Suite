using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Golem_Mining_Suite
{
	public partial class LocationWindow : Window
	{
		public class LocationData
		{
			public string LocationName { get; set; }
			public string Chance { get; set; }
			public string System { get; set; }
		}

		private string currentDepositName;
		private List<LocationData> allLocations;
		private string currentFilter = "All";

		public LocationWindow(string depositName)
		{
			InitializeComponent();
			currentDepositName = depositName;
			TitleText.Text = $"Ore Deposits for {depositName}";
			LoadLocations(depositName);
		}

		private void LoadLocations(string depositName)
		{
			var depositLocations = new Dictionary<string, List<LocationData>>
			{
				["Atacamite"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Ita", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "17%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "18%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "21%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "15%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "17%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "16%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "15%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "16%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "18%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "22%", System = "Pyro" }
				},

				["Felsic"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Microtech", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "4%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "11%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "5%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "6%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "7%", System = "Pyro" }
				},

				["Gneiss"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Microtech", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "12%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "10%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "15%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Terminus", Chance = "11%", System = "Pyro" }
				},

				["Granite"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Hurston", Chance = "23%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "21%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "18%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "17%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "18%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "16%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "17%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "22%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "16%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "15%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "16%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "18%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Terminus", Chance = "17%", System = "Pyro" }
				},

				["Igneous"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Hurston", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "6%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "6%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "6%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "12%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "2%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "9%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "9%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "6%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "4%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "9%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "8%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "3%", System = "Pyro" }
				},

				["Obsidian"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Hurston", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "12%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "4%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "11%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "5%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "6%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "14%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "7%", System = "Pyro" },
					new LocationData { LocationName = "Vuur", Chance = "19%", System = "Pyro" }
				},

				["Quartzite"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Microtech", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "6%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "6%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "11%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "3%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "8%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "7%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "4%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "8%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "11%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "8%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "11%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "15%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "17%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "19%", System = "Pyro" }
				},

				["Shale"] = new List<LocationData>
				{
                    // Stanton
                    new LocationData { LocationName = "Hurston", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Calliope", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Euterpe", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Lyria", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Wala", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Daymar", Chance = "11%", System = "Stanton" },
					new LocationData { LocationName = "Cellin", Chance = "12%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "11%", System = "Stanton" },
					new LocationData { LocationName = "Aberdeen", Chance = "10%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "12%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "9%", System = "Stanton" },
					new LocationData { LocationName = "Ita", Chance = "12%", System = "Stanton" },
                    // Pyro
                    new LocationData { LocationName = "Pyro I", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Monox", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Bloom", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "9%", System = "Pyro" },
					new LocationData { LocationName = "Ignis", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Vatra", Chance = "5%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "10%", System = "Pyro" },
					new LocationData { LocationName = "Fairo", Chance = "8%", System = "Pyro" },
					new LocationData { LocationName = "Fuego", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Vuur", Chance = "12%", System = "Pyro" },
					new LocationData { LocationName = "Terminus", Chance = "10%", System = "Pyro" }
				}
			};

			if (depositLocations.ContainsKey(depositName))
			{
				allLocations = depositLocations[depositName];
				ApplyFilter();
			}
			else
			{
				LocationsGrid.ItemsSource = new List<LocationData>
				{
					new LocationData { LocationName = "No data available", Chance = "N/A", System = "N/A" }
				};
			}
		}

		private void FilterButton_Click(object sender, RoutedEventArgs e)
		{
			Button clickedButton = sender as Button;
			currentFilter = clickedButton.Tag.ToString();

			AllButton.Style = (Style)FindResource("FilterButton");
			StantonButton.Style = (Style)FindResource("FilterButton");
			PyroButton.Style = (Style)FindResource("FilterButton");

			clickedButton.Style = (Style)FindResource("ActiveFilterButton");

			ApplyFilter();
		}

		private void ApplyFilter()
		{
			if (allLocations == null) return;

			List<LocationData> filteredLocations;

			if (currentFilter == "All")
			{
				filteredLocations = allLocations;
			}
			else
			{
				filteredLocations = allLocations.Where(l => l.System == currentFilter).ToList();
			}

			var sortedLocations = filteredLocations
				.OrderByDescending(l => int.Parse(l.Chance.Replace("%", "")))
				.ToList();

			LocationsGrid.ItemsSource = sortedLocations;
		}
	}
}