using System.Collections.Generic;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System.Linq;

namespace Golem_Mining_Suite.Services
{
    public class CommodityDataService : ICommodityDataService
    {
        private readonly UEXService _uexService;
        private List<CommodityData>? _cachedCommodities;
        private bool _useApi = true;

        public CommodityDataService(UEXService uexService)
        {
            _uexService = uexService;
        }

        public async Task<List<CommodityData>> GetAllCommoditiesAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[CommodityDataService] Getting commodities. UseAPI: {_useApi}, IsConfigured: {_uexService.IsConfigured}");
            
            if (_useApi && _uexService.IsConfigured)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CommodityDataService] Fetching from UEX API...");
                    var apiData = await _uexService.GetCommoditiesAsync();
                    if (apiData != null && apiData.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CommodityDataService] API success. Count: {apiData.Count}");
                        _cachedCommodities = apiData;
                        return apiData;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CommodityDataService] API returned null or empty.");
                    }
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommodityDataService] API failed: {ex.Message}");
                    // Fallback to static if API fails
                }
            }
            else
            {
                 System.Diagnostics.Debug.WriteLine("[CommodityDataService] API not configured or disabled. Using static.");
            }

            if (_cachedCommodities == null || _cachedCommodities.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[CommodityDataService] Loading static data.");
                _cachedCommodities = GetStaticCommodities();
            }

            return _cachedCommodities;
        }

        public async Task<CommodityData?> GetCommodityDetailsAsync(string commodityName)
        {
            var all = await GetAllCommoditiesAsync();
            return all.FirstOrDefault(c => c.Name.Equals(commodityName, System.StringComparison.OrdinalIgnoreCase) || 
                                         c.Code.Equals(commodityName, System.StringComparison.OrdinalIgnoreCase));
        }

        private List<CommodityData> GetStaticCommodities()
        {
            // Comprehensive list of Star Citizen commodities
            return new List<CommodityData>
            {
                new CommodityData { Name = "Agricium", Code = "agricium", Type = "Mineral", AveragePriceBuy = 25.00, AveragePriceSell = 27.50 },
                new CommodityData { Name = "Agricultural Supplies", Code = "agricultural_supplies", Type = "Commodity", AveragePriceBuy = 1.20, AveragePriceSell = 1.45 },
                new CommodityData { Name = "Aluminum", Code = "aluminum", Type = "Mineral", AveragePriceBuy = 1.20, AveragePriceSell = 1.35 },
                new CommodityData { Name = "Astatine", Code = "astatine", Type = "Mineral", AveragePriceBuy = 8.50, AveragePriceSell = 10.00 },
                new CommodityData { Name = "Beryl", Code = "beryl", Type = "Mineral", AveragePriceBuy = 4.20, AveragePriceSell = 5.10 },
                new CommodityData { Name = "Borase", Code = "borase", Type = "Mineral", AveragePriceBuy = 31.00, AveragePriceSell = 35.00 },
                new CommodityData { Name = "Chlorine", Code = "chlorine", Type = "Mineral", AveragePriceBuy = 1.50, AveragePriceSell = 1.80 },
                new CommodityData { Name = "Construction Materials", Code = "construction_materials", Type = "Commodity", AveragePriceBuy = 6000, AveragePriceSell = 6500 }, // Box price vs SCU?
                new CommodityData { Name = "Copper", Code = "copper", Type = "Mineral", AveragePriceBuy = 5.50, AveragePriceSell = 6.20 },
                new CommodityData { Name = "Corundum", Code = "corundum", Type = "Mineral", AveragePriceBuy = 2.50, AveragePriceSell = 2.90 },
                new CommodityData { Name = "Diamond", Code = "diamond", Type = "Mineral", AveragePriceBuy = 6.30, AveragePriceSell = 7.35 },
                new CommodityData { Name = "Distilled Spirits", Code = "distilled_spirits", Type = "Commodity", AveragePriceBuy = 4.80, AveragePriceSell = 5.95 },
                new CommodityData { Name = "Fluorine", Code = "fluorine", Type = "Mineral", AveragePriceBuy = 2.70, AveragePriceSell = 3.10 },
                new CommodityData { Name = "Gold", Code = "gold", Type = "Mineral", AveragePriceBuy = 5.80, AveragePriceSell = 6.90 },
                new CommodityData { Name = "Hephaestanite", Code = "hephaestanite", Type = "Mineral", AveragePriceBuy = 14.50, AveragePriceSell = 16.00 },
                new CommodityData { Name = "Hydrogen", Code = "hydrogen", Type = "Gas", AveragePriceBuy = 0.90, AveragePriceSell = 1.10 },
                new CommodityData { Name = "Iodine", Code = "iodine", Type = "Mineral", AveragePriceBuy = 0.40, AveragePriceSell = 0.50 },
                new CommodityData { Name = "Iron", Code = "iron", Type = "Mineral", AveragePriceBuy = 1.40, AveragePriceSell = 1.80 },
                new CommodityData { Name = "Laranite", Code = "laranite", Type = "Mineral", AveragePriceBuy = 27.50, AveragePriceSell = 32.50 },
                new CommodityData { Name = "Medical Supplies", Code = "medical_supplies", Type = "Commodity", AveragePriceBuy = 17.50, AveragePriceSell = 19.80 },
                new CommodityData { Name = "Processed Food", Code = "processed_food", Type = "Commodity", AveragePriceBuy = 1.30, AveragePriceSell = 1.55 },
                new CommodityData { Name = "Quantanium", Code = "quantanium", Type = "Mineral", AveragePriceBuy = 80.00, AveragePriceSell = 88.00, IsHighlighted = true },
                new CommodityData { Name = "Quartz", Code = "quartz", Type = "Mineral", AveragePriceBuy = 1.40, AveragePriceSell = 1.65 },
                new CommodityData { Name = "RMC", Code = "recycled_material_composite", Type = "Commodity", AveragePriceBuy = 13500, AveragePriceSell = 15000 },
                new CommodityData { Name = "Scrap", Code = "scrap", Type = "Commodity", AveragePriceBuy = 1.40, AveragePriceSell = 1.75 },
                new CommodityData { Name = "Stims", Code = "stims", Type = "Commodity", AveragePriceBuy = 3.20, AveragePriceSell = 3.80 },
                new CommodityData { Name = "Titanium", Code = "titanium", Type = "Mineral", AveragePriceBuy = 8.10, AveragePriceSell = 9.30 },
                new CommodityData { Name = "Tungsten", Code = "tungsten", Type = "Mineral", AveragePriceBuy = 3.80, AveragePriceSell = 4.40 },
                new CommodityData { Name = "Waste", Code = "waste", Type = "Commodity", AveragePriceBuy = 0.01, AveragePriceSell = 0.05 },
                new CommodityData { Name = "Widow", Code = "widow", Type = "Drug", AveragePriceBuy = 150.00, AveragePriceSell = 220.00, IsHighlighted = true }
            }.OrderBy(c => c.Name).ToList();
        }
    }
}
