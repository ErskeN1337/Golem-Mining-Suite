using System.Collections.Generic;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IMiningDataService
    {
        // Surface Mining
        Dictionary<string, List<LocationData>> GetSurfaceMineralToDepositMapping();
        Dictionary<string, List<LocationData>> GetAllSurfaceDepositLocations();
        string GetSurfaceDepositSignature(string depositName, string system);
        List<ClusterRockInfo> GetSurfaceClusterRockData(string depositName);
        List<MineralData> GetFeaturedSurfaceMinerals();

        // Asteroid Mining
        Dictionary<string, List<LocationData>> GetAsteroidMineralToOreTypeMapping();
        Dictionary<string, List<LocationData>> GetAsteroidOreTypeLocations();
        string GetAsteroidOreTypeSignature(string oreTypeName);
        List<ClusterRockInfo> GetAsteroidClusterRockData(string oreTypeName);
        List<AsteroidMineralData> GetAsteroidMinerals();

        // ROC Mining
        List<string> GetROCRockTypes();
    }
}
