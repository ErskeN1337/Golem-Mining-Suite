using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Golem_Mining_Suite.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Golem_Mining_Suite.Data.AsteroidLocations
{
    /// <summary>
    /// Loads <see cref="AsteroidLocationData"/> records from CSV files shipped in
    /// <c>Assets/Data/AsteroidLocations/</c>. Uses CsvHelper so quoted fields and
    /// embedded commas are handled correctly (previously was a naive Split).
    /// </summary>
    public static class AsteroidLocationLoader
    {
        public static List<AsteroidLocationData> LoadFromCSV(string system)
        {
            string csvPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "Data", "AsteroidLocations", $"{system}.csv");

            if (!File.Exists(csvPath))
            {
                Log.Warning("AsteroidLocationLoader: CSV not found for system '{System}' at {Path}", system, csvPath);
                return new List<AsteroidLocationData>();
            }

            var locations = new List<AsteroidLocationData>();

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    // Tolerate extra/missing columns and empty rows in the source CSVs.
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null,
                    BadDataFound = null,
                    IgnoreBlankLines = true,
                    TrimOptions = TrimOptions.Trim,
                };

                using var reader = new StreamReader(csvPath);
                using var csv = new CsvReader(reader, config);

                csv.Context.RegisterClassMap<AsteroidLocationCsvMap>();

                foreach (var row in csv.GetRecords<AsteroidLocationCsvRow>())
                {
                    if (string.IsNullOrWhiteSpace(row.LocationName) || string.IsNullOrWhiteSpace(row.System))
                    {
                        continue;
                    }

                    var location = new AsteroidLocationData
                    {
                        LocationName = row.LocationName,
                        System = row.System,
                    };

                    AddIfPresent(location.OreTypeSpawnRates, "C-Type", row.CType);
                    AddIfPresent(location.OreTypeSpawnRates, "E-Type", row.EType);
                    AddIfPresent(location.OreTypeSpawnRates, "I-Type", row.IType);
                    AddIfPresent(location.OreTypeSpawnRates, "M-Type", row.MType);
                    AddIfPresent(location.OreTypeSpawnRates, "P-Type", row.PType);
                    AddIfPresent(location.OreTypeSpawnRates, "Q-Type", row.QType);
                    AddIfPresent(location.OreTypeSpawnRates, "S-Type", row.SType);

                    locations.Add(location);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AsteroidLocationLoader: failed to parse CSV for system '{System}' at {Path}", system, csvPath);
                return new List<AsteroidLocationData>();
            }

            return locations;
        }

        private static void AddIfPresent(IDictionary<string, string> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target[key] = value!;
            }
        }

        /// <summary>
        /// Row DTO mirroring the CSV header. Kept separate from
        /// <see cref="AsteroidLocationData"/> because CsvHelper cannot bind a
        /// <see cref="Dictionary{TKey,TValue}"/> directly.
        /// </summary>
        private sealed class AsteroidLocationCsvRow
        {
            public string LocationName { get; set; } = string.Empty;
            public string System { get; set; } = string.Empty;
            public string? CType { get; set; }
            public string? EType { get; set; }
            public string? IType { get; set; }
            public string? MType { get; set; }
            public string? PType { get; set; }
            public string? QType { get; set; }
            public string? SType { get; set; }
        }

        /// <summary>
        /// Maps the CSV header (which uses hyphenated names like "C-Type") to
        /// C# property names that cannot contain hyphens.
        /// </summary>
        private sealed class AsteroidLocationCsvMap : ClassMap<AsteroidLocationCsvRow>
        {
            public AsteroidLocationCsvMap()
            {
                Map(m => m.LocationName).Name("LocationName");
                Map(m => m.System).Name("System");
                Map(m => m.CType).Name("C-Type");
                Map(m => m.EType).Name("E-Type");
                Map(m => m.IType).Name("I-Type");
                Map(m => m.MType).Name("M-Type");
                Map(m => m.PType).Name("P-Type");
                Map(m => m.QType).Name("Q-Type");
                Map(m => m.SType).Name("S-Type");
            }
        }
    }
}
