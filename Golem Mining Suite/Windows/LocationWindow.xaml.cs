using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Data;

namespace Golem_Mining_Suite
{
	public partial class LocationWindow : Window
	{
		private string currentDepositName;
		private List<LocationData> allLocations;
		private string currentFilter = "All";
		private bool isMineral;
		private bool isAsteroidMode;
		private string currentLocationType = "All";
		private double minDepositPercent = 0;
		private double minMineralPercent = 0;
		private string locationSearchText = "";

		public LocationWindow(string name, bool isMineralSearch = false, bool asteroidMode = false)
		{
			InitializeComponent();
			isMineral = isMineralSearch;
			isAsteroidMode = asteroidMode;

			// Show/hide filters based on mode FIRST
			if (isAsteroidMode)
			{
				LocationTypePanel.Visibility = Visibility.Visible;
				PlanetFilterPanel.Visibility = Visibility.Collapsed;
			}
			else
			{
				LocationTypePanel.Visibility = Visibility.Collapsed;
				PlanetFilterPanel.Visibility = Visibility.Visible;
			}

			// Then load data
			if (isMineral)
			{
				if (isAsteroidMode)
				{
					TitleText.Text = $"Best Ore Types for {name}";
					LoadAsteroidMineralLocations(name);
				}
				else
				{
					TitleText.Text = $"Best Ore Deposits for {name}";
					LoadMineralDeposits(name);
				}
			}
			else
			{
				currentDepositName = name;
				if (isAsteroidMode)
				{
					TitleText.Text = $"Ore Type: {name}";
					LoadAsteroidOreTypeLocations(name);
				}
				else
				{
					TitleText.Text = $"Ore Deposits for {name}";
					LoadLocations(name);
				}
			}

			// Load planet filter for surface mining
			if (!isAsteroidMode)
			{
				LoadPlanetFilter();
			}
		}

		// SURFACE MINING - Load mineral deposits
		private void LoadMineralDeposits(string mineralName)
		{
			var mineralDepositData = SurfaceMiningData.GetMineralToDepositMapping();
			var depositLocationData = SurfaceMiningData.GetAllDepositLocations();

			if (!mineralDepositData.ContainsKey(mineralName))
			{
				ShowNoDataMessage();
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
							Signature = SurfaceMiningData.GetDepositSignature(depositName, planet.System),
							DepositType = depositName
						});
					}
				}
			}

			allLocations = planetMineralData;
			ApplyFilter();
		}

		// ASTEROID MINING - Load mineral ore types
		private void LoadAsteroidMineralLocations(string mineralName)
		{
			var mineralOreTypeData = AsteroidMiningData.GetMineralToOreTypeMapping();
			var oreTypeLocationData = AsteroidMiningData.GetOreTypeLocations();

			if (!mineralOreTypeData.ContainsKey(mineralName))
			{
				ShowNoDataMessage();
				return;
			}

			var oreTypesWithMineral = mineralOreTypeData[mineralName];
			var asteroidMineralData = new List<LocationData>();

			foreach (var oreType in oreTypesWithMineral)
			{
				string oreTypeName = oreType.LocationName;
				string mineralPercentage = oreType.Chance;

				if (oreTypeLocationData.ContainsKey(oreTypeName))
				{
					var locationsWithOreType = oreTypeLocationData[oreTypeName];

					foreach (var location in locationsWithOreType)
					{
						double oreTypeSpawnRate = double.Parse(location.Chance.Replace("%", ""));

						asteroidMineralData.Add(new LocationData
						{
							LocationName = $"{location.LocationName} - {oreTypeName}",
							DepositChance = location.Chance,
							MineralChance = mineralPercentage,
							System = location.System,
							SortValue = oreTypeSpawnRate,
							Signature = AsteroidMiningData.GetOreTypeSignature(oreTypeName),
							DepositType = oreTypeName
						});
					}
				}
			}

			allLocations = asteroidMineralData;
			ApplyFilter();
		}

		// SURFACE MINING - Load deposit locations
		private void LoadLocations(string depositName)
		{
			var depositLocations = SurfaceMiningData.GetAllDepositLocations();

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
					Signature = SurfaceMiningData.GetDepositSignature(depositName, loc.System),
					DepositType = depositName
				}).ToList();
				ApplyFilter();
			}
		}

		// ASTEROID MINING - Load ore type locations
		private void LoadAsteroidOreTypeLocations(string oreTypeName)
		{
			var oreTypeLocations = AsteroidMiningData.GetOreTypeLocations();

			if (oreTypeLocations.ContainsKey(oreTypeName))
			{
				allLocations = oreTypeLocations[oreTypeName].Select(loc => new LocationData
				{
					LocationName = loc.LocationName,
					Chance = loc.Chance,
					DepositChance = loc.Chance,
					MineralChance = "-",
					System = loc.System,
					SortValue = double.Parse(loc.Chance.Replace("%", "")),
					Signature = AsteroidMiningData.GetOreTypeSignature(oreTypeName),
					DepositType = oreTypeName
				}).ToList();
				ApplyFilter();
			}
		}

		private void ShowNoDataMessage()
		{
			LocationsGrid.ItemsSource = new List<LocationData>
			{
				new LocationData
				{
					LocationName = "No data available",
					DepositChance = "N/A",
					MineralChance = "N/A",
					System = "N/A",
					SortValue = 0
				}
			};
		}

		// Load planet dropdown
		private void LoadPlanetFilter()
		{
			if (allLocations == null) return;

			var planets = new List<string> { "All" };

			planets.AddRange(allLocations
				.Select(l => l.LocationName.Split('-')[0].Trim())
				.Distinct()
				.OrderBy(p => p)
				.ToList());

			PlanetComboBox.ItemsSource = planets;
			PlanetComboBox.SelectedIndex = 0;
		}

		// System Filter
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

		// Location Type Filter
		private void LocationTypeFilter_Click(object sender, RoutedEventArgs e)
		{
			Button clickedButton = sender as Button;
			currentLocationType = clickedButton.Tag.ToString();

			// Reset button styles
			AllTypeButton.Style = (Style)FindResource("FilterButton");
			MiningBaseButton.Style = (Style)FindResource("FilterButton");
			LagrangeButton.Style = (Style)FindResource("FilterButton");
			BeltButton.Style = (Style)FindResource("FilterButton");

			// Set active button
			clickedButton.Style = (Style)FindResource("ActiveFilterButton");

			ApplyFilter();
		}

		// Planet filter changed
		private void PlanetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PlanetComboBox.SelectedItem == null || allLocations == null) return;
			ApplyFilter();
		}

		// Location Search
		private void LocationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			locationSearchText = LocationSearchBox.Text.ToLower();
			ApplyFilter();
		}

		// Min Deposit % Filter
		private void MinDepositBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (double.TryParse(MinDepositBox.Text, out double value))
			{
				minDepositPercent = value;
				ApplyFilter();
			}
			else if (string.IsNullOrWhiteSpace(MinDepositBox.Text))
			{
				minDepositPercent = 0;
				ApplyFilter();
			}
		}

		// Min Mineral % Filter
		private void MinMineralBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (double.TryParse(MinMineralBox.Text, out double value))
			{
				minMineralPercent = value;
				ApplyFilter();
			}
			else if (string.IsNullOrWhiteSpace(MinMineralBox.Text))
			{
				minMineralPercent = 0;
				ApplyFilter();
			}
		}

		// Apply All Filters
		private void ApplyFilter()
		{
			if (allLocations == null) return;

			List<LocationData> filteredLocations = allLocations;

			// 1. Filter by System
			if (currentFilter != "All")
			{
				filteredLocations = filteredLocations.Where(l => l.System == currentFilter).ToList();
			}

			// 2. Filter by Location Type (Asteroid mode only)
			if (isAsteroidMode && currentLocationType != "All")
			{
				filteredLocations = filteredLocations.Where(l =>
				{
					string locationName = l.LocationName.ToLower();
					switch (currentLocationType)
					{
						case "MiningBase":
							return locationName.Contains("mining base");
						case "Lagrange":
							// Matches: HUR L1, MIC L2, ARC L3, CRU L4, etc.
							return System.Text.RegularExpressions.Regex.IsMatch(locationName, @"\s+l[1-5](\s|$)");
						case "Belt":
							return locationName.Contains("belt");
						default:
							return true;
					}
				}).ToList();
			}

			// 3. Filter by Planet (Surface mode only)
			if (!isAsteroidMode && PlanetComboBox != null && PlanetComboBox.SelectedItem != null)
			{
				string selectedPlanet = PlanetComboBox.SelectedItem.ToString();
				if (selectedPlanet != "All")
				{
					filteredLocations = filteredLocations.Where(l =>
						l.LocationName.ToLower().StartsWith(selectedPlanet.ToLower())
					).ToList();
				}
			}

			// 4. Filter by Location Name Search (both modes)
			if (!string.IsNullOrWhiteSpace(locationSearchText))
			{
				filteredLocations = filteredLocations.Where(l =>
					l.LocationName.ToLower().Contains(locationSearchText)
				).ToList();
			}

			// 5. Filter by Min Deposit %
			if (minDepositPercent > 0)
			{
				filteredLocations = filteredLocations.Where(l =>
				{
					string depositChance = l.DepositChance?.Replace("%", "") ?? "0";
					return double.TryParse(depositChance, out double deposit) && deposit >= minDepositPercent;
				}).ToList();
			}

			// 6. Filter by Min Mineral %
			if (minMineralPercent > 0 && isMineral)
			{
				filteredLocations = filteredLocations.Where(l =>
				{
					string mineralChance = l.MineralChance?.Replace("%", "") ?? "0";
					return double.TryParse(mineralChance, out double mineral) && mineral >= minMineralPercent;
				}).ToList();
			}

			// Sort by spawn rate (descending)
			var sortedLocations = filteredLocations
				.OrderByDescending(l => l.SortValue)
				.ToList();

			LocationsGrid.ItemsSource = sortedLocations;
		}

		// Signature Popup
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

			// Get cluster rock data based on mode
			List<ClusterRockInfo> clusterData;

			if (isAsteroidMode)
			{
				clusterData = AsteroidMiningData.GetClusterRockData(locationData.DepositType);
			}
			else
			{
				clusterData = SurfaceMiningData.GetClusterRockData(locationData.DepositType);
			}

			ClusterRocksPanel.ItemsSource = clusterData;
			SignaturePopup.IsOpen = true;
		}
	}
}