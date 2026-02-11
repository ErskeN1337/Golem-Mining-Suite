using Golem_Mining_Suite.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Golem_Mining_Suite.Data.AsteroidLocations
{
	public static class AsteroidLocationLoader
	{
		public static List<AsteroidLocationData> LoadFromCSV(string system)
		{
			string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Assets/Data/AsteroidLocations/{system}.csv");

			if (!File.Exists(csvPath))
			{
				return new List<AsteroidLocationData>();
			}

			var locations = new List<AsteroidLocationData>();

			try
			{
				var lines = File.ReadAllLines(csvPath).Skip(1); // Skip header

				foreach (var line in lines)
				{
					var values = line.Split(',');
					if (values.Length < 9) continue;

					var location = new AsteroidLocationData
					{
						LocationName = values[0],
						System = values[1],
						OreTypeSpawnRates = new Dictionary<string, string>()
					};

					// Add ore types if they have values
					if (!string.IsNullOrWhiteSpace(values[2])) location.OreTypeSpawnRates["C-Type"] = values[2];
					if (!string.IsNullOrWhiteSpace(values[3])) location.OreTypeSpawnRates["E-Type"] = values[3];
					if (!string.IsNullOrWhiteSpace(values[4])) location.OreTypeSpawnRates["I-Type"] = values[4];
					if (!string.IsNullOrWhiteSpace(values[5])) location.OreTypeSpawnRates["M-Type"] = values[5];
					if (!string.IsNullOrWhiteSpace(values[6])) location.OreTypeSpawnRates["P-Type"] = values[6];
					if (!string.IsNullOrWhiteSpace(values[7])) location.OreTypeSpawnRates["Q-Type"] = values[7];
					if (!string.IsNullOrWhiteSpace(values[8])) location.OreTypeSpawnRates["S-Type"] = values[8];

					locations.Add(location);
				}
			}
			catch (Exception)
			{
				// Silently fail if CSV can't be loaded
				return new List<AsteroidLocationData>();
			}

			return locations;
		}

	}

}
