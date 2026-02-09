namespace Golem_Mining_Suite.Models
{
	public class LocationData
	{
		public string LocationName { get; set; } = "";
		public string Chance { get; set; } = "";
		public string? DepositChance { get; set; }
		public string? MineralChance { get; set; }
		public string System { get; set; } = "";
		public double SortValue { get; set; }
		public string Signature { get; set; } = "";
		public string? DepositType { get; set; }
	}

	public class ClusterRockInfo
	{
		public required string Size { get; set; }
		public required string Percentage { get; set; }
	}

	// NEW: Better model for asteroid locations
	public class AsteroidLocationData
	{
		public required string LocationName { get; set; }
		public required string System { get; set; }
		public Dictionary<string, string> OreTypeSpawnRates { get; set; } // "C-Type" -> "12%"

		public AsteroidLocationData()
		{
			OreTypeSpawnRates = new Dictionary<string, string>();
		}
	}
}