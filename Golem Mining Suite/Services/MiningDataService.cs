using System.Collections.Generic;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Data;
using Golem_Mining_Suite.Services.Interfaces;

namespace Golem_Mining_Suite.Services
{
    public class MiningDataService : IMiningDataService
    {
        // Surface Mining
        public Dictionary<string, List<LocationData>> GetSurfaceMineralToDepositMapping()
        {
            return SurfaceMiningData.GetMineralToDepositMapping();
        }

        public Dictionary<string, List<LocationData>> GetAllSurfaceDepositLocations()
        {
            return SurfaceMiningData.GetAllDepositLocations();
        }

        public string GetSurfaceDepositSignature(string depositName, string system)
        {
            return SurfaceMiningData.GetDepositSignature(depositName, system);
        }

        public List<ClusterRockInfo> GetSurfaceClusterRockData(string depositName)
        {
            return SurfaceMiningData.GetClusterRockData(depositName);
        }

        public List<MineralData> GetFeaturedSurfaceMinerals()
        {
            var mapping = SurfaceMiningData.GetMineralToDepositMapping();
            var result = new List<MineralData>();

            foreach (var kvp in mapping)
            {
                var mineral = new MineralData
                {
                    MineralName = kvp.Key,
                    OreSources = new List<OreSource>()
                };

                foreach (var loc in kvp.Value)
                {
                    // Parse percentage string "26%" to double 26
                    double pct = 0;
                    if (double.TryParse(loc.Chance.Replace("%", ""), out double parsed))
                    {
                        pct = parsed;
                    }

                    mineral.OreSources.Add(new OreSource
                    {
                        OreName = loc.LocationName,
                        Percentage = pct
                    });
                }
                
                // Sort ore sources by percentage descending
                mineral.OreSources.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
                result.Add(mineral);
            }

            // Sort minerals alphabetically or by importance? Alphabetical seems fine.
            result.Sort((a, b) => a.MineralName.CompareTo(b.MineralName));
            
            return result;
        }

        // Asteroid Mining
        public Dictionary<string, List<LocationData>> GetAsteroidMineralToOreTypeMapping()
        {
            return AsteroidMiningData.GetMineralToOreTypeMapping();
        }

        public Dictionary<string, List<LocationData>> GetAsteroidOreTypeLocations()
        {
            return AsteroidMiningData.GetOreTypeLocations();
        }

        public string GetAsteroidOreTypeSignature(string oreTypeName)
        {
            return AsteroidMiningData.GetOreTypeSignature(oreTypeName);
        }

        public List<ClusterRockInfo> GetAsteroidClusterRockData(string oreTypeName)
        {
            return AsteroidMiningData.GetClusterRockData(oreTypeName);
        }

        public List<AsteroidMineralData> GetAsteroidMinerals()
        {
            return new List<AsteroidMineralData>
            {
                // Quantanium
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "C-Type", Percentage = 6 },
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "E-Type", Percentage = 5 },
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "M-Type", Percentage = 7 },
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "P-Type", Percentage = 2 },
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "Q-Type", Percentage = 5 },
                new AsteroidMineralData { MineralName = "Quantanium", OreType = "S-Type", Percentage = 3 },

                // Bexalite
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "C-Type", Percentage = 18 },
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "E-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "M-Type", Percentage = 9 },
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "P-Type", Percentage = 20 },
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "Q-Type", Percentage = 13 },
                new AsteroidMineralData { MineralName = "Bexalite", OreType = "S-Type", Percentage = 13 },

                // Taranite
                new AsteroidMineralData { MineralName = "Taranite", OreType = "C-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Taranite", OreType = "E-Type", Percentage = 11 },
                new AsteroidMineralData { MineralName = "Taranite", OreType = "M-Type", Percentage = 14 },
                new AsteroidMineralData { MineralName = "Taranite", OreType = "P-Type", Percentage = 22 },
                new AsteroidMineralData { MineralName = "Taranite", OreType = "Q-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Taranite", OreType = "S-Type", Percentage = 19 },

                // Gold
                new AsteroidMineralData { MineralName = "Gold", OreType = "C-Type", Percentage = 8 },
                new AsteroidMineralData { MineralName = "Gold", OreType = "E-Type", Percentage = 29 },
                new AsteroidMineralData { MineralName = "Gold", OreType = "M-Type", Percentage = 25 },
                new AsteroidMineralData { MineralName = "Gold", OreType = "P-Type", Percentage = 6 },
                new AsteroidMineralData { MineralName = "Gold", OreType = "Q-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Gold", OreType = "S-Type", Percentage = 6 },

                // Hephaestanite
                new AsteroidMineralData { MineralName = "Hephaestanite", OreType = "C-Type", Percentage = 45 },
                new AsteroidMineralData { MineralName = "Hephaestanite", OreType = "M-Type", Percentage = 57 },

                // Beryl
                new AsteroidMineralData { MineralName = "Beryl", OreType = "E-Type", Percentage = 8 },
                new AsteroidMineralData { MineralName = "Beryl", OreType = "S-Type", Percentage = 38 },

                // Laranite
                new AsteroidMineralData { MineralName = "Laranite", OreType = "P-Type", Percentage = 47 },
                new AsteroidMineralData { MineralName = "Laranite", OreType = "Q-Type", Percentage = 27 },
                new AsteroidMineralData { MineralName = "Laranite", OreType = "S-Type", Percentage = 18 },

                // Agricium
                new AsteroidMineralData { MineralName = "Agricium", OreType = "M-Type", Percentage = 57 },

                // Tungsten
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "C-Type", Percentage = 22 },
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "E-Type", Percentage = 22 },
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "M-Type", Percentage = 10 },
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "P-Type", Percentage = 14 },
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "Q-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Tungsten", OreType = "S-Type", Percentage = 15 },

                // Titanium
                new AsteroidMineralData { MineralName = "Titanium", OreType = "C-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Titanium", OreType = "E-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Titanium", OreType = "M-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Titanium", OreType = "P-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Titanium", OreType = "Q-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Titanium", OreType = "S-Type", Percentage = 15 },

                // Iron
                new AsteroidMineralData { MineralName = "Iron", OreType = "C-Type", Percentage = 10 },
                new AsteroidMineralData { MineralName = "Iron", OreType = "E-Type", Percentage = 11 },
                new AsteroidMineralData { MineralName = "Iron", OreType = "M-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Iron", OreType = "P-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Iron", OreType = "Q-Type", Percentage = 10 },
                new AsteroidMineralData { MineralName = "Iron", OreType = "S-Type", Percentage = 7 },

                // Quartz
                new AsteroidMineralData { MineralName = "Quartz", OreType = "C-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Quartz", OreType = "E-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Quartz", OreType = "M-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Quartz", OreType = "P-Type", Percentage = 8 },
                new AsteroidMineralData { MineralName = "Quartz", OreType = "Q-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Quartz", OreType = "S-Type", Percentage = 13 },

                // Corundum
                new AsteroidMineralData { MineralName = "Corundum", OreType = "C-Type", Percentage = 15 },
                new AsteroidMineralData { MineralName = "Corundum", OreType = "E-Type", Percentage = 14 },
                new AsteroidMineralData { MineralName = "Corundum", OreType = "M-Type", Percentage = 22 },
                new AsteroidMineralData { MineralName = "Corundum", OreType = "P-Type", Percentage = 18 },
                new AsteroidMineralData { MineralName = "Corundum", OreType = "Q-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Corundum", OreType = "S-Type", Percentage = 9 },

                // Copper
                new AsteroidMineralData { MineralName = "Copper", OreType = "C-Type", Percentage = 4 },
                new AsteroidMineralData { MineralName = "Copper", OreType = "E-Type", Percentage = 4 },
                new AsteroidMineralData { MineralName = "Copper", OreType = "M-Type", Percentage = 11 },
                new AsteroidMineralData { MineralName = "Copper", OreType = "P-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Copper", OreType = "Q-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Copper", OreType = "S-Type", Percentage = 9 },

                // Aluminum
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "C-Type", Percentage = 14 },
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "E-Type", Percentage = 19 },
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "M-Type", Percentage = 16 },
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "P-Type", Percentage = 20 },
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "Q-Type", Percentage = 14 },
                new AsteroidMineralData { MineralName = "Aluminum", OreType = "S-Type", Percentage = 63 },

                // New minerals for Pyro/Nyx
                // Silicene
                new AsteroidMineralData { MineralName = "Silicene", OreType = "C-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Silicene", OreType = "E-Type", Percentage = 2 },
                new AsteroidMineralData { MineralName = "Silicene", OreType = "M-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Silicene", OreType = "P-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Silicene", OreType = "Q-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Silicene", OreType = "S-Type", Percentage = 1 },

                // Rhodite
                new AsteroidMineralData { MineralName = "Rhodite", OreType = "C-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Rhodite", OreType = "E-Type", Percentage = 1 },
                new AsteroidMineralData { MineralName = "Rhodite", OreType = "M-Type", Percentage = 1 },

                // Ice
                new AsteroidMineralData { MineralName = "Ice", OreType = "I-Type", Percentage = 81 },

                // Torite
                new AsteroidMineralData { MineralName = "Torite", OreType = "M-Type", Percentage = 5 },
                new AsteroidMineralData { MineralName = "Torite", OreType = "P-Type", Percentage = 1 },

                // Lindranium
                new AsteroidMineralData { MineralName = "Lindranium", OreType = "M-Type", Percentage = 25 },

                // Tin
                new AsteroidMineralData { MineralName = "Tin", OreType = "P-Type", Percentage = 12 },
                new AsteroidMineralData { MineralName = "Tin", OreType = "Q-Type", Percentage = 13 },
                new AsteroidMineralData { MineralName = "Tin", OreType = "S-Type", Percentage = 13 },

                // Silicon
                new AsteroidMineralData { MineralName = "Silicon", OreType = "C-Type", Percentage = 6 },
                new AsteroidMineralData { MineralName = "Silicon", OreType = "E-Type", Percentage = 9 },
                new AsteroidMineralData { MineralName = "Silicon", OreType = "P-Type", Percentage = 12 }
            };
        }

        // ROC Mining
        public List<string> GetROCRockTypes()
        {
            return ROCMiningData.GetAllRockTypes();
        }
    }
}
