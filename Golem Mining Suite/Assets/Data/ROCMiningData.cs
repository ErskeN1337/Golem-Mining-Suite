using System.Collections.Generic;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Data
{
	public static class ROCMiningData
	{
		public static Dictionary<string, List<LocationData>> GetRockTypeLocations()
		{
			return new Dictionary<string, List<LocationData>>
			{
				// Janalite (Column 1)
				["Janalite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "41%", System = "Stanton", SortValue = 41, DepositChance = "41%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "15%", System = "Stanton", SortValue = 15, DepositChance = "15%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Magda", Chance = "11%", System = "Stanton", SortValue = 11, DepositChance = "11%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Calliope", Chance = "3%", System = "Stanton", SortValue = 3, DepositChance = "3%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "12%", System = "Stanton", SortValue = 12, DepositChance = "12%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Lyria", Chance = "4%", System = "Stanton", SortValue = 4, DepositChance = "4%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Wala", Chance = "4%", System = "Stanton", SortValue = 4, DepositChance = "4%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "25%", System = "Stanton", SortValue = 25, DepositChance = "25%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "6%", System = "Stanton", SortValue = 6, DepositChance = "6%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Yela", Chance = "13%", System = "Stanton", SortValue = 13, DepositChance = "13%", MineralChance = "100%" }
				},

				// Hadanite (Column 2)
				["Hadanite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "31%", System = "Stanton", SortValue = 31, DepositChance = "31%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "34%", System = "Stanton", SortValue = 34, DepositChance = "34%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Ita", Chance = "20%", System = "Stanton", SortValue = 20, DepositChance = "20%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Magda", Chance = "30%", System = "Stanton", SortValue = 30, DepositChance = "30%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Calliope", Chance = "6%", System = "Stanton", SortValue = 6, DepositChance = "6%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "6%", System = "Stanton", SortValue = 6, DepositChance = "6%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Lyria", Chance = "8%", System = "Stanton", SortValue = 8, DepositChance = "8%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Wala", Chance = "17%", System = "Stanton", SortValue = 17, DepositChance = "17%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "17%", System = "Stanton", SortValue = 17, DepositChance = "17%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "5%", System = "Stanton", SortValue = 5, DepositChance = "5%", MineralChance = "100%" }
				},

				// Feynmaline (Column 3)
				["Feynmaline"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "2%", System = "Stanton", SortValue = 2, DepositChance = "2%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "1%", System = "Stanton", SortValue = 1, DepositChance = "1%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Calliope", Chance = "6%", System = "Stanton", SortValue = 6, DepositChance = "6%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "7%", System = "Stanton", SortValue = 7, DepositChance = "7%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Euterpe", Chance = "13%", System = "Stanton", SortValue = 13, DepositChance = "13%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Lyria", Chance = "24%", System = "Stanton", SortValue = 24, DepositChance = "24%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Wala", Chance = "26%", System = "Stanton", SortValue = 26, DepositChance = "26%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "11%", System = "Stanton", SortValue = 11, DepositChance = "11%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "28%", System = "Stanton", SortValue = 28, DepositChance = "28%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Yela", Chance = "5%", System = "Stanton", SortValue = 5, DepositChance = "5%", MineralChance = "100%" }
				},

				// Aphorite (Column 4)
				["Aphorite"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "3%", System = "Stanton", SortValue = 3, DepositChance = "3%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "16%", System = "Stanton", SortValue = 16, DepositChance = "16%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Magda", Chance = "5%", System = "Stanton", SortValue = 5, DepositChance = "5%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Calliope", Chance = "39%", System = "Stanton", SortValue = 39, DepositChance = "39%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "10%", System = "Stanton", SortValue = 10, DepositChance = "10%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Euterpe", Chance = "3%", System = "Stanton", SortValue = 3, DepositChance = "3%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Lyria", Chance = "12%", System = "Stanton", SortValue = 12, DepositChance = "12%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Wala", Chance = "35%", System = "Stanton", SortValue = 35, DepositChance = "35%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "28%", System = "Stanton", SortValue = 28, DepositChance = "28%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "31%", System = "Stanton", SortValue = 31, DepositChance = "31%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Yela", Chance = "18%", System = "Stanton", SortValue = 18, DepositChance = "18%", MineralChance = "100%" }
				},

				// Beradom (Column 5)
				["Beradom"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "10%", System = "Stanton", SortValue = 10, DepositChance = "10%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "10%", System = "Stanton", SortValue = 10, DepositChance = "10%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Ita", Chance = "20%", System = "Stanton", SortValue = 20, DepositChance = "20%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Magda", Chance = "20%", System = "Stanton", SortValue = 20, DepositChance = "20%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Calliope", Chance = "45%", System = "Stanton", SortValue = 45, DepositChance = "45%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "16%", System = "Stanton", SortValue = 16, DepositChance = "16%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Euterpe", Chance = "70%", System = "Stanton", SortValue = 70, DepositChance = "70%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Wala", Chance = "17%", System = "Stanton", SortValue = 17, DepositChance = "17%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "8%", System = "Stanton", SortValue = 8, DepositChance = "8%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "11%", System = "Stanton", SortValue = 11, DepositChance = "11%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Yela", Chance = "28%", System = "Stanton", SortValue = 28, DepositChance = "28%", MineralChance = "100%" }
				},

				// Dolivine (Column 6)
				["Dolivine"] = new List<LocationData>
				{
					new LocationData { LocationName = "Hurston - Aberdeen", Chance = "13%", System = "Stanton", SortValue = 13, DepositChance = "13%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Arial", Chance = "22%", System = "Stanton", SortValue = 22, DepositChance = "22%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Ita", Chance = "60%", System = "Stanton", SortValue = 60, DepositChance = "60%", MineralChance = "100%" },
					new LocationData { LocationName = "Hurston - Magda", Chance = "34%", System = "Stanton", SortValue = 34, DepositChance = "34%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Clio", Chance = "48%", System = "Stanton", SortValue = 48, DepositChance = "48%", MineralChance = "100%" },
					new LocationData { LocationName = "ArcCorp - Euterpe", Chance = "13%", System = "Stanton", SortValue = 13, DepositChance = "13%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Lyria", Chance = "52%", System = "Stanton", SortValue = 52, DepositChance = "52%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Daymar", Chance = "11%", System = "Stanton", SortValue = 11, DepositChance = "11%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Cellin", Chance = "19%", System = "Stanton", SortValue = 19, DepositChance = "19%", MineralChance = "100%" },
					new LocationData { LocationName = "Crusader - Yela", Chance = "38%", System = "Stanton", SortValue = 38, DepositChance = "38%", MineralChance = "100%" }
				}
			};
		}

		// Get list of all rock types for display
		public static List<string> GetAllRockTypes()
		{
			return new List<string>
			{
				"Janalite",
				"Hadanite",
				"Feynmaline",
				"Aphorite",
				"Beradom",
				"Dolivine"
			};
		}
	}
}