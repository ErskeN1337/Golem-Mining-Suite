namespace Golem_Mining_Suite.Models
{
	public class LocationData
	{
		public required string LocationName { get; set; }
		public required string Chance { get; set; }
		public required string DepositChance { get; set; }
		public required string MineralChance { get; set; }
		public required string System { get; set; }
		public double SortValue { get; set; }
		public required string Signature { get; set; }
		public required string DepositType { get; set; }
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