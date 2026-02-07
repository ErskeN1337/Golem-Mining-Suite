using System.Collections.Generic;
using System.Linq;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Data.AsteroidLocations;

namespace Golem_Mining_Suite.Data
{
	public static class AsteroidMiningData
	{
		// Mineral to Ore Type mapping (unchanged)
		public static Dictionary<string, List<LocationData>> GetMineralToOreTypeMapping()
		{
			return new Dictionary<string, List<LocationData>>
			{
				["Quantanium"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "7%" },
					new LocationData { LocationName = "C-Type", Chance = "6%" },
					new LocationData { LocationName = "E-Type", Chance = "5%" },
					new LocationData { LocationName = "Q-Type", Chance = "5%" },
					new LocationData { LocationName = "S-Type", Chance = "3%" },
					new LocationData { LocationName = "P-Type", Chance = "2%" }
				},
				["Bexalite"] = new List<LocationData>
				{
					new LocationData { LocationName = "P-Type", Chance = "20%" },
					new LocationData { LocationName = "C-Type", Chance = "18%" },
					new LocationData { LocationName = "S-Type", Chance = "13%" },
					new LocationData { LocationName = "Q-Type", Chance = "13%" },
					new LocationData { LocationName = "E-Type", Chance = "12%" },
					new LocationData { LocationName = "M-Type", Chance = "9%" }
				},
				["Taranite"] = new List<LocationData>
				{
					new LocationData { LocationName = "P-Type", Chance = "22%" },
					new LocationData { LocationName = "S-Type", Chance = "19%" },
					new LocationData { LocationName = "Q-Type", Chance = "16%" },
					new LocationData { LocationName = "M-Type", Chance = "14%" },
					new LocationData { LocationName = "C-Type", Chance = "12%" },
					new LocationData { LocationName = "E-Type", Chance = "11%" }
				},
				["Gold"] = new List<LocationData>
				{
					new LocationData { LocationName = "E-Type", Chance = "29%" },
					new LocationData { LocationName = "M-Type", Chance = "25%" },
					new LocationData { LocationName = "Q-Type", Chance = "12%" },
					new LocationData { LocationName = "C-Type", Chance = "8%" },
					new LocationData { LocationName = "S-Type", Chance = "6%" },
					new LocationData { LocationName = "P-Type", Chance = "6%" }
				},
				["Hephaestanite"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "57%" },
					new LocationData { LocationName = "C-Type", Chance = "45%" }
				},
				["Beryl"] = new List<LocationData>
				{
					new LocationData { LocationName = "S-Type", Chance = "38%" },
					new LocationData { LocationName = "E-Type", Chance = "8%" }
				},
				["Laranite"] = new List<LocationData>
				{
					new LocationData { LocationName = "P-Type", Chance = "47%" },
					new LocationData { LocationName = "Q-Type", Chance = "27%" },
					new LocationData { LocationName = "S-Type", Chance = "18%" }
				},
				["Agricium"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "57%" }
				},
				["Tungsten"] = new List<LocationData>
				{
					new LocationData { LocationName = "C-Type", Chance = "22%" },
					new LocationData { LocationName = "E-Type", Chance = "22%" },
					new LocationData { LocationName = "S-Type", Chance = "15%" },
					new LocationData { LocationName = "Q-Type", Chance = "15%" },
					new LocationData { LocationName = "P-Type", Chance = "14%" },
					new LocationData { LocationName = "M-Type", Chance = "10%" }
				},
				["Titanium"] = new List<LocationData>
				{
					new LocationData { LocationName = "C-Type", Chance = "15%" },
					new LocationData { LocationName = "M-Type", Chance = "15%" },
					new LocationData { LocationName = "P-Type", Chance = "15%" },
					new LocationData { LocationName = "S-Type", Chance = "15%" },
					new LocationData { LocationName = "E-Type", Chance = "12%" },
					new LocationData { LocationName = "Q-Type", Chance = "12%" }
				},
				["Iron"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "15%" },
					new LocationData { LocationName = "P-Type", Chance = "12%" },
					new LocationData { LocationName = "E-Type", Chance = "11%" },
					new LocationData { LocationName = "C-Type", Chance = "10%" },
					new LocationData { LocationName = "Q-Type", Chance = "10%" },
					new LocationData { LocationName = "S-Type", Chance = "7%" }
				},
				["Quartz"] = new List<LocationData>
				{
					new LocationData { LocationName = "Q-Type", Chance = "16%" },
					new LocationData { LocationName = "S-Type", Chance = "13%" },
					new LocationData { LocationName = "C-Type", Chance = "12%" },
					new LocationData { LocationName = "E-Type", Chance = "12%" },
					new LocationData { LocationName = "M-Type", Chance = "12%" },
					new LocationData { LocationName = "P-Type", Chance = "8%" }
				},
				["Corundum"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "22%" },
					new LocationData { LocationName = "P-Type", Chance = "18%" },
					new LocationData { LocationName = "Q-Type", Chance = "16%" },
					new LocationData { LocationName = "C-Type", Chance = "15%" },
					new LocationData { LocationName = "E-Type", Chance = "14%" },
					new LocationData { LocationName = "S-Type", Chance = "9%" }
				},
				["Copper"] = new List<LocationData>
				{
					new LocationData { LocationName = "P-Type", Chance = "16%" },
					new LocationData { LocationName = "Q-Type", Chance = "16%" },
					new LocationData { LocationName = "M-Type", Chance = "11%" },
					new LocationData { LocationName = "S-Type", Chance = "9%" },
					new LocationData { LocationName = "C-Type", Chance = "4%" },
					new LocationData { LocationName = "E-Type", Chance = "4%" }
				},
				["Aluminum"] = new List<LocationData>
				{
					new LocationData { LocationName = "S-Type", Chance = "63%" },
					new LocationData { LocationName = "P-Type", Chance = "20%" },
					new LocationData { LocationName = "E-Type", Chance = "19%" },
					new LocationData { LocationName = "M-Type", Chance = "16%" },
					new LocationData { LocationName = "C-Type", Chance = "14%" },
					new LocationData { LocationName = "Q-Type", Chance = "14%" }
				},
				// NEW MINERALS (Pyro/Nyx specific)
				["Silicene"] = new List<LocationData>
				{
					new LocationData { LocationName = "E-Type", Chance = "2%" },
					new LocationData { LocationName = "C-Type", Chance = "1%" },
					new LocationData { LocationName = "M-Type", Chance = "1%" },
					new LocationData { LocationName = "P-Type", Chance = "1%" },
					new LocationData { LocationName = "Q-Type", Chance = "1%" },
					new LocationData { LocationName = "S-Type", Chance = "1%" }
				},
				["Rhodite"] = new List<LocationData>
				{
					new LocationData { LocationName = "C-Type", Chance = "1%" },
					new LocationData { LocationName = "E-Type", Chance = "1%" },
					new LocationData { LocationName = "M-Type", Chance = "1%" }
				},
				["Ice"] = new List<LocationData>
				{
					new LocationData { LocationName = "I-Type", Chance = "81%" }
				},
				["Torite"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "5%" },
					new LocationData { LocationName = "P-Type", Chance = "1%" }
				},
				["Lindranium"] = new List<LocationData>
				{
					new LocationData { LocationName = "M-Type", Chance = "25%" }
				},
				["Tin"] = new List<LocationData>
				{
					new LocationData { LocationName = "Q-Type", Chance = "13%" },
					new LocationData { LocationName = "S-Type", Chance = "13%" },
					new LocationData { LocationName = "P-Type", Chance = "12%" }
				},
				["Silicon"] = new List<LocationData>
				{
					new LocationData { LocationName = "P-Type", Chance = "12%" },
					new LocationData { LocationName = "E-Type", Chance = "9%" },
					new LocationData { LocationName = "C-Type", Chance = "6%" }
				}
			};
		}

		// NEW: Get ore type locations from all systems
		public static Dictionary<string, List<LocationData>> GetOreTypeLocations()
		{
			var result = new Dictionary<string, List<LocationData>>();

			// Get all asteroid locations from all systems
			var allLocations = new List<AsteroidLocationData>();
			allLocations.AddRange(AsteroidLocationLoader.LoadFromCSV("Stanton"));
			allLocations.AddRange(AsteroidLocationLoader.LoadFromCSV("Pyro"));
			allLocations.AddRange(AsteroidLocationLoader.LoadFromCSV("Nyx")); // Uncommented!

			// Group by ore type
			var oreTypes = new[] { "C-Type", "E-Type", "I-Type", "M-Type", "P-Type", "Q-Type", "S-Type" };

			foreach (var oreType in oreTypes)
			{
				var locationsForOreType = new List<LocationData>();

				foreach (var location in allLocations)
				{
					if (location.OreTypeSpawnRates.ContainsKey(oreType))
					{
						string spawnRate = location.OreTypeSpawnRates[oreType];

						// Only add if spawn rate is not 0%
						if (spawnRate != "0%")
						{
							locationsForOreType.Add(new LocationData
							{
								LocationName = location.LocationName,
								Chance = spawnRate,
								System = location.System
							});
						}
					}
				}

				result[oreType] = locationsForOreType;
			}

			return result;
		}

		// Get signatures for ore types
		public static string GetOreTypeSignature(string oreTypeName)
		{
			return oreTypeName switch
			{
				"C-Type" => "1700",
				"E-Type" => "1900",
				"I-Type" => "1660",
				"M-Type" => "1850",
				"P-Type" => "1750",
				"Q-Type" => "1870",
				"S-Type" => "1720",
				_ => ""
			};
		}

		// Get cluster rock calculations
		public static List<ClusterRockInfo> GetClusterRockData(string oreTypeName)
		{
			int baseSignature = oreTypeName switch
			{
				"C-Type" => 1700,
				"E-Type" => 1900,
				"I-Type" => 1660,
				"M-Type" => 1850,
				"P-Type" => 1750,
				"Q-Type" => 1870,
				"S-Type" => 1720,
				_ => 0
			};

			if (baseSignature == 0)
				return new List<ClusterRockInfo>();

			return new List<ClusterRockInfo>
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
		}
	}

}