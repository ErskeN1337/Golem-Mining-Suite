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
			public string DepositChance { get; set; }
			public string MineralChance { get; set; }
			public string System { get; set; }
			public double SortValue { get; set; }
			public string Signature { get; set; }
			public string DepositType { get; set; }
		}

		private class ClusterRockInfo
		{
			public string Size { get; set; }
			public string Percentage { get; set; }
		}

		private string currentDepositName;
		private List<LocationData> allLocations;
		private string currentFilter = "All";
		private bool isMineral;

		public LocationWindow(string name, bool isMineralSearch = false)
		{
			InitializeComponent();
			isMineral = isMineralSearch;

			if (isMineral)
			{
				TitleText.Text = $"Best Ore Deposits for {name}";
				LoadMineralDeposits(name);
			}
			else
			{
				currentDepositName = name;
				TitleText.Text = $"Ore Deposits for {name}";
				LoadLocations(name);
			}
		}

		private void LoadMineralDeposits(string mineralName)
		{
			var mineralDepositData = GetMineralToDepositMapping();
			var depositLocationData = GetAllDepositLocations();

			if (!mineralDepositData.ContainsKey(mineralName))
			{
				LocationsGrid.ItemsSource = new List<LocationData>
				{
					new LocationData { LocationName = "No data available", DepositChance = "N/A", MineralChance = "N/A", System = "N/A", SortValue = 0 }
				};
				return;
			}

			var depositsWithMineral = mineralDepositData[mineralName];
			var planetMineralData = new List<LocationData>();

			foreach (var deposit in depositsWithMineral)
			{
				string depositName = deposit.LocationName;
				string mineralPercentage = deposit.Chance;

				if (depositLocationData.ContainsKey(depositName))
				{
					var planetsWithDeposit = depositLocationData[depositName];

					foreach (var planet in planetsWithDeposit)
					{
						double depositSpawnRate = double.Parse(planet.Chance.Replace("%", ""));

						planetMineralData.Add(new LocationData
						{
							LocationName = $"{planet.LocationName} - {depositName}",
							DepositChance = planet.Chance,
							MineralChance = mineralPercentage,
							System = planet.System,
							SortValue = depositSpawnRate,
							Signature = GetDepositSignature(depositName, planet.System),
							DepositType = depositName
						});
					}
				}
			}

			allLocations = planetMineralData;
			ApplyFilter();
		}

		private Dictionary<string, List<LocationData>> GetMineralToDepositMapping()
		{
			return new Dictionary<string, List<LocationData>>
			{
				["Gold"] = new List<LocationData>
				{
					new LocationData { LocationName = "Shale", Chance = "26%" },
					new LocationData { LocationName = "Granite", Chance = "25%" },
					new LocationData { LocationName = "Atacamite", Chance = "21%" },
					new LocationData { LocationName = "Felsic", Chance = "20%" },
					new LocationData { LocationName = "Igneous", Chance = "19%" },
					new LocationData { LocationName = "Obsidian", Chance = "13%" }
				},
				["Quantanium"] = new List<LocationData>
				{
					new LocationData { LocationName = "Granite", Chance = "9%" },
					new LocationData { LocationName = "Shale", Chance = "7%" },
					new LocationData { LocationName = "Igneous", Chance = "7%" },
					new LocationData { LocationName = "Obsidian", Chance = "7%" }
				},
				["Bexalite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Quartzite", Chance = "13%" },
					new LocationData { LocationName = "Gneiss", Chance = "13%" },
					new LocationData { LocationName = "Felsic", Chance = "12%" }
				},
				["Taranite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Felsic", Chance = "20%" },
					new LocationData { LocationName = "Gneiss", Chance = "19%" },
					new LocationData { LocationName = "Quartzite", Chance = "19%" }
				},
				["Laranite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Igneous", Chance = "39%" },
					new LocationData { LocationName = "Granite", Chance = "37%" },
					new LocationData { LocationName = "Shale", Chance = "37%" }
				},
				["Agricium"] = new List<LocationData>
				{
					new LocationData { LocationName = "Atacamite", Chance = "26%" },
					new LocationData { LocationName = "Granite", Chance = "25%" },
					new LocationData { LocationName = "Igneous", Chance = "25%" }
				},
				["Hephaestanite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Felsic", Chance = "48%" },
					new LocationData { LocationName = "Quartzite", Chance = "40%" },
					new LocationData { LocationName = "Gneiss", Chance = "39%" }
				},
				["Beryl"] = new List<LocationData>
				{
					new LocationData { LocationName = "Felsic", Chance = "48%" },
					new LocationData { LocationName = "Obsidian", Chance = "35%" }
				},
				["Diamond"] = new List<LocationData>
				{
					new LocationData { LocationName = "Granite", Chance = "9%" },
					new LocationData { LocationName = "Felsic", Chance = "8%" },
					new LocationData { LocationName = "Gneiss", Chance = "8%" }
				},
				["Borase"] = new List<LocationData>
				{
					new LocationData { LocationName = "Granite", Chance = "43%" },
					new LocationData { LocationName = "Igneous", Chance = "25%" }
				},
				["Tungsten"] = new List<LocationData>
				{
					new LocationData { LocationName = "Igneous", Chance = "12%" },
					new LocationData { LocationName = "Atacamite", Chance = "10%" },
					new LocationData { LocationName = "Shale", Chance = "10%" }
				},
				["Titanium"] = new List<LocationData>
				{
					new LocationData { LocationName = "Granite", Chance = "17%" },
					new LocationData { LocationName = "Shale", Chance = "17%" },
					new LocationData { LocationName = "Atacamite", Chance = "10%" }
				},
				["Iron"] = new List<LocationData>
				{
					new LocationData { LocationName = "Atacamite", Chance = "34%" },
					new LocationData { LocationName = "Quartzite", Chance = "23%" },
					new LocationData { LocationName = "Felsic", Chance = "13%" }
				},
				["Quartz"] = new List<LocationData>
				{
					new LocationData { LocationName = "Felsic", Chance = "22%" },
					new LocationData { LocationName = "Gneiss", Chance = "14%" },
					new LocationData { LocationName = "Obsidian", Chance = "14%" }
				},
				["Corundum"] = new List<LocationData>
				{
					new LocationData { LocationName = "Shale", Chance = "21%" },
					new LocationData { LocationName = "Atacamite", Chance = "18%" },
					new LocationData { LocationName = "Felsic", Chance = "15%" }
				},
				["Copper"] = new List<LocationData>
				{
					new LocationData { LocationName = "Gneiss", Chance = "6%" },
					new LocationData { LocationName = "Obsidian", Chance = "5%" },
					new LocationData { LocationName = "Shale", Chance = "5%" }
				},
				["Aluminum"] = new List<LocationData>
				{
					new LocationData { LocationName = "Granite", Chance = "42%" },
					new LocationData { LocationName = "Quartzite", Chance = "41%" },
					new LocationData { LocationName = "Obsidian", Chance = "37%" }
				}
			};
		}

		private Dictionary<string, List<LocationData>> GetAllDepositLocations()
		{
			return new Dictionary<string, List<LocationData>>
			{
				["Granite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston", Chance = "23%", System = "Stanton" },
					new LocationData { LocationName = "Monox", Chance = "22%", System = "Pyro" },
					new LocationData { LocationName = "Calliope", Chance = "21%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "19%", System = "Stanton" },
					new LocationData { LocationName = "Arial", Chance = "19%", System = "Stanton" }
				},
				["Shale"] = new List<LocationData>
				{
					new LocationData { LocationName = "Calliope", Chance = "15%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Clio", Chance = "13%", System = "Stanton" },
					new LocationData { LocationName = "Bloom", Chance = "13%", System = "Pyro" }
				},
				["Atacamite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Monox", Chance = "22%", System = "Pyro" },
					new LocationData { LocationName = "Calliope", Chance = "21%", System = "Stanton" },
					new LocationData { LocationName = "Microtech", Chance = "19%", System = "Stanton" }
				},
				["Felsic"] = new List<LocationData>
				{
					new LocationData { LocationName = "Calliope", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Magda", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Vatra", Chance = "14%", System = "Pyro" }
				},
				["Igneous"] = new List<LocationData>
				{
					new LocationData { LocationName = "Magda", Chance = "14%", System = "Stanton" },
					new LocationData { LocationName = "Ignis", Chance = "13%", System = "Pyro" },
					new LocationData { LocationName = "Cellin", Chance = "12%", System = "Stanton" }
				},
				["Obsidian"] = new List<LocationData>
				{
					new LocationData { LocationName = "Vuur", Chance = "19%", System = "Pyro" },
					new LocationData { LocationName = "Calliope", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Hurston", Chance = "15%", System = "Stanton" }
				},
				["Quartzite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Fuego", Chance = "19%", System = "Pyro" },
					new LocationData { LocationName = "Adir", Chance = "17%", System = "Pyro" },
					new LocationData { LocationName = "Pyro IV", Chance = "15%", System = "Pyro" }
				},
				["Gneiss"] = new List<LocationData>
				{
					new LocationData { LocationName = "Daymar", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Yela", Chance = "16%", System = "Stanton" },
					new LocationData { LocationName = "Pyro I", Chance = "15%", System = "Pyro" }
				}
			};
		}

		private void LoadLocations(string depositName)
		{
			var depositLocations = GetAllDepositLocations();

			if (depositLocations.ContainsKey(depositName))
			{
				allLocations = depositLocations[depositName].Select(loc => new LocationData
				{
					LocationName = loc.LocationName,
					Chance = loc.Chance,
					DepositChance = loc.Chance,
					MineralChance = "-",
					System = loc.System,
					SortValue = double.Parse(loc.Chance.Replace("%", "")),
					Signature = GetDepositSignature(depositName, loc.System),
					DepositType = depositName
				}).ToList();
				ApplyFilter();
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
				.OrderByDescending(l => l.SortValue)
				.ToList();

			LocationsGrid.ItemsSource = sortedLocations;
		}

		private string GetDepositSignature(string depositName, string system)
		{
			var signatures = new Dictionary<string, string>
			{
				["Atacamite-Stanton"] = "1800",
				["Atacamite-Pyro"] = "1806",
				["Felsic-Stanton"] = "1778",
				["Felsic-Pyro"] = "1778",
				["Gneiss-Stanton"] = "1848",
				["Gneiss-Pyro"] = "1846",
				["Granite-Stanton"] = "1928",
				["Granite-Pyro"] = "1928",
				["Igneous-Stanton"] = "1950",
				["Igneous-Pyro"] = "1950",
				["Obsidian-Stanton"] = "1790",
				["Obsidian-Pyro"] = "1790",
				["Quartzite-Stanton"] = "1820",
				["Quartzite-Pyro"] = "1820",
				["Shale-Stanton"] = "1730",
				["Shale-Pyro"] = "1730"
			};

			string key = $"{depositName}-{system}";
			return signatures.ContainsKey(key) ? signatures[key] : "";
		}

		private void Signature_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			var textBlock = sender as TextBlock;
			if (textBlock?.DataContext is LocationData locationData)
			{
				ShowSignaturePopup(locationData);
			}
		}

		private void Signature_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			SignaturePopup.IsOpen = false;
		}

		private void ShowSignaturePopup(LocationData locationData)
		{
			if (string.IsNullOrEmpty(locationData.DepositType))
				return;

			PopupTitle.Text = locationData.DepositType;

			var clusterData = GetClusterRockData(locationData.DepositType);
			ClusterRocksPanel.ItemsSource = clusterData;

			SignaturePopup.IsOpen = true;
		}

		private List<ClusterRockInfo> GetClusterRockData(string depositType)
		{
			// Get base signature value
			int baseSignature = 0;
			switch (depositType)
			{
				case "Shale": baseSignature = 1730; break;
				case "Granite": baseSignature = 1928; break;
				case "Atacamite": baseSignature = 1800; break;
				case "Felsic": baseSignature = 1778; break;
				case "Gneiss": baseSignature = 1848; break;
				case "Igneous": baseSignature = 1950; break;
				case "Obsidian": baseSignature = 1790; break;
				case "Quartzite": baseSignature = 1820; break;
				default: return new List<ClusterRockInfo>();
			}

			// Calculate multipliers
			var clusterData = new List<ClusterRockInfo>
	{
		new ClusterRockInfo { Size = "0", Percentage = "0" },
		new ClusterRockInfo { Size = "2x", Percentage = (baseSignature * 2).ToString() },
		new ClusterRockInfo { Size = "4x", Percentage = (baseSignature * 4).ToString() },
		new ClusterRockInfo { Size = "6x", Percentage = (baseSignature * 6).ToString() },
		new ClusterRockInfo { Size = "8x", Percentage = (baseSignature * 8).ToString() },
		new ClusterRockInfo { Size = "10x", Percentage = (baseSignature * 10).ToString() },
		new ClusterRockInfo { Size = "12x", Percentage = (baseSignature * 12).ToString() },
		new ClusterRockInfo { Size = "14x", Percentage = (baseSignature * 14).ToString() },
		new ClusterRockInfo { Size = "16x", Percentage = (baseSignature * 16).ToString() }
	};

			return clusterData;
		}
	}
}