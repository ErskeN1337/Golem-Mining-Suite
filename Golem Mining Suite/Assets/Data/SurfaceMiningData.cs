using System.Collections.Generic;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Data
{
	public static class SurfaceMiningData
	{
		// Mineral to Deposit mapping (static, doesn't change per system)
		public static Dictionary<string, List<LocationData>> GetMineralToDepositMapping()
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

		// Get deposit locations (all systems combined)
		public static Dictionary<string, List<LocationData>> GetAllDepositLocations()
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

		// Get signatures for deposits
		public static string GetDepositSignature(string depositName, string system)
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

		// Get cluster rock calculations
		public static List<ClusterRockInfo> GetClusterRockData(string depositName)
		{
			int baseSignature = depositName switch
			{
				"Shale" => 1730,
				"Granite" => 1928,
				"Atacamite" => 1800,
				"Felsic" => 1778,
				"Gneiss" => 1848,
				"Igneous" => 1950,
				"Obsidian" => 1790,
				"Quartzite" => 1820,
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