using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Golem_Mining_Suite.Services
{
    public class PriceService : IPriceService
    {
        public event EventHandler? PricesUpdated;
        public event EventHandler<bool>? LinkStatusChanged;
        public bool IsLiveConnected { get; private set; }
        private List<PriceData> _liveOverrides = new List<PriceData>();

        private Dictionary<int, string> _terminalToSystem = new Dictionary<int, string>();
        private List<TerminalInfo> _cachedTerminals = new List<TerminalInfo>();

        public async Task<List<TerminalInfo>> GetTerminalsAsync()
        {
            if (_cachedTerminals.Any()) return _cachedTerminals;

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var response = await client.GetStringAsync("https://uexcorp.space/api/terminals");
                    var jsonDoc = JsonDocument.Parse(response);
                    var terminals = jsonDoc.RootElement.GetProperty("data");

                    foreach (var terminal in terminals.EnumerateArray())
                    {
                        int id = terminal.GetProperty("id").GetInt32();
                        
                        // Filter by type (Mining/Commodities only)
                        string? type = null;
                        if (terminal.TryGetProperty("type", out var typeElement))
                        {
                            type = typeElement.GetString();
                        }

                        if (type != "commodity" && type != "commodity_raw" && type != "refinery")
                            continue;

                        // Use 'displayname' (e.g. "Grim HEX", "Lorville") instead of specific terminal name
                        string name = "";
                        if (terminal.TryGetProperty("displayname", out var dnElement))
                        {
                            name = dnElement.GetString() ?? "";
                        }
                        
                        if (string.IsNullOrWhiteSpace(name))
                        {
                             name = terminal.GetProperty("name").GetString() ?? "";
                        }
                        
                        string starSystem = terminal.GetProperty("star_system_name").GetString() ?? "";
                        
                        // Avoid duplicates (e.g. multiple shops at same station)
                        if (_cachedTerminals.Any(t => t.Name == name && t.StarSystem == starSystem))
                            continue;
                            
                        var info = new TerminalInfo { Id = id, Name = name, StarSystem = starSystem };
                        _cachedTerminals.Add(info);
                        
                        // Also populate the mapping for legacy use
                        if (!_terminalToSystem.ContainsKey(id))
                            _terminalToSystem[id] = starSystem;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback or empty
            }
            
            return _cachedTerminals.OrderBy(t => t.Name).ToList();
        }

        public async Task<Dictionary<int, string>> GetTerminalMappingAsync()
        {
            if (_terminalToSystem.Count > 0) return _terminalToSystem;

            var mapping = new Dictionary<int, string>();

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var response = await client.GetStringAsync("https://uexcorp.space/api/terminals");
                    var jsonDoc = JsonDocument.Parse(response);
                    var terminals = jsonDoc.RootElement.GetProperty("data");

                    foreach (var terminal in terminals.EnumerateArray())
                    {
                        int id = terminal.GetProperty("id").GetInt32();
                        string? starSystem = terminal.GetProperty("star_system_name").GetString();
                        if (starSystem != null)
                            mapping[id] = starSystem;
                    }
                }
            }
            catch (Exception)
            {
                // Log error or handle gracefully
                // For now return empty or basic mapping
            }

            _terminalToSystem = mapping;
            return mapping;
        }

        public async Task<List<PriceData>> GetMineralPricesAsync()
        {
            return await GetPricesInternalAsync(onlyMinerals: true);
        }

        public async Task<List<PriceData>> GetAllCommodityPricesAsync()
        {
            return await GetPricesInternalAsync(onlyMinerals: false);
        }

        private async Task<List<PriceData>> GetPricesInternalAsync(bool onlyMinerals)
        {
            var priceList = new List<PriceData>();

            // Ensure we have terminal mapping
            if (_terminalToSystem.Count == 0)
            {
                await GetTerminalMappingAsync();
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    // Fetch API prices
                    var response = await client.GetStringAsync("https://uexcorp.space/api/commodities_prices_all");
                    var jsonDoc = JsonDocument.Parse(response);
                    var pricesData = jsonDoc.RootElement.GetProperty("data");

                    foreach (var priceEntry in pricesData.EnumerateArray())
                    {
                        var commodityName = priceEntry.GetProperty("commodity_name").GetString() ?? "";
                        var terminalName = priceEntry.GetProperty("terminal_name").GetString() ?? "";
                        
                        // Price Sell = Terminal SELLS to us (Cost)
                        // Price Buy = Terminal BUYS from us (Value)
                        double priceSell = 0;
                        double priceBuy = 0;

                        if (priceEntry.TryGetProperty("price_sell", out var psVal)) 
                            priceSell = psVal.ValueKind == JsonValueKind.Number ? psVal.GetDouble() : 0;
                            
                        if (priceEntry.TryGetProperty("price_buy", out var pbVal)) 
                            priceBuy = pbVal.ValueKind == JsonValueKind.Number ? pbVal.GetDouble() : 0;

                        int terminalId = priceEntry.GetProperty("id_terminal").GetInt32();
                        string starSystem = _terminalToSystem.ContainsKey(terminalId) ? _terminalToSystem[terminalId] : "Unknown";

                        int scuMax = 100;
                        int scu = 0;

                        if (priceEntry.TryGetProperty("scu", out JsonElement scuElement))
                             scu = scuElement.ValueKind == JsonValueKind.Number ? scuElement.GetInt32() : 0;
                        if (priceEntry.TryGetProperty("scu_max", out JsonElement scuMaxElement))
                             scuMax = scuMaxElement.ValueKind == JsonValueKind.Number ? scuMaxElement.GetInt32() : 0;

                        // Include if there is ANY activity (Buy OR Sell)
                        if (priceSell <= 0 && priceBuy <= 0)
                            continue;

                        var displayName = MapCommodityName(commodityName);

                        if (onlyMinerals && !IsMineralName(displayName))
                            continue;

                        double inventoryPercent = scuMax > 0 ? (double)scu / scuMax * 100 : 0;
                        string demand = inventoryPercent < 50 ? "High" : "Low";

                        // Check for LIVE override
                        var overrideData = _liveOverrides.FirstOrDefault(o => o.MineralName == displayName && o.BestLocation == terminalName);
                        if (overrideData != null)
                        {
                            priceList.Add(overrideData);
                        }
                        else
                        {
                            // Determine primary "Price" text based on context
                            // For Market view, usually "Sell" (Cost) is primary, or show range?
                            // Let's stick to "Sell" (Cost) for numeric sort if available, else "Buy"
                            double primaryPrice = priceSell > 0 ? priceSell : priceBuy;

                            priceList.Add(new PriceData
                            {
                                MineralName = displayName,
                                Price = $"{primaryPrice:N2} aUEC",
                                NumericPrice = primaryPrice,
                                UnitBuyPrice = priceBuy,
                                UnitSellPrice = priceSell,
                                BestLocation = terminalName,
                                Demand = demand,
                                StarSystem = starSystem,
                                LastUpdatedText = "API"
                            });
                        }
                    }
                }
                
                // Merge overrides
                foreach (var live in _liveOverrides)
                {
                    if (onlyMinerals && !IsMineralName(live.MineralName)) continue;

                    if (!priceList.Any(p => p.MineralName == live.MineralName && p.BestLocation == live.BestLocation))
                    {
                        priceList.Add(live);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PriceService] Error fetching prices: {ex.Message}");
                // If we have some partial data, maybe return it? 
                // But usually exception means we want full fallback if list is empty.
                if (priceList.Count == 0) return GetFallbackPrices();
            }

            // Fallback if API returned nothing (e.g. empty list or parsing failure without exception)
            if (priceList.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[PriceService] API returned 0 items. Using fallback.");
                return GetFallbackPrices();
            }

            return priceList.OrderByDescending(p => p.NumericPrice).ToList();
        }

        public void UpdateWithLiveData(object? sender, TerminalData liveData)
        {
            AddLivePriceOverride(liveData);
            PricesUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void AddLivePriceOverride(TerminalData data)
        {
             if (data.PriceSell <= 0) return;
             
             var displayName = MapCommodityName(data.CommodityName);
             if (!IsMineralName(displayName)) return;
             
             // Create PriceData
             var priceData = new PriceData
             {
                 MineralName = displayName,
                 Price = $"{data.PriceSell:N0} aUEC",
                 NumericPrice = data.PriceSell,
                 BestLocation = data.TerminalName,
                 Demand = "Live", 
                 StarSystem = data.StarSystem,
                 LastUpdated = data.CapturedAt,
                 LastUpdatedText = data.CapturedAt.ToString("HH:mm:ss")
             };
             
             // Remove existing override for same location/mineral
             var existing = _liveOverrides.FirstOrDefault(p => p.MineralName == displayName && p.BestLocation == priceData.BestLocation);
             if (existing != null)
             {
                 _liveOverrides.Remove(existing);
             }
             _liveOverrides.Add(priceData);
        }

        public void SetLiveConnectionStatus(bool connected)
        {
            IsLiveConnected = connected;
            LinkStatusChanged?.Invoke(this, connected);
        }
        private bool IsMineral(string name)
        {
            var minerals = new HashSet<string>
            {
                "Quantanium", "Bexalite", "Taranite", "Laranite", "Agricium",
                "Hephaestanite", "Beryl", "Gold", "Borase", "Tungsten",
                "Titanium", "Iron", "Quartz", "Copper", "Corundum", "Aluminum",
                "Hadanite", "Dolivine", "Aphorite", "Janalite"
            };

            return minerals.Contains(name);
        }

        private bool IsMineralName(string name) => IsMineral(name);

        private string MapCommodityName(string name)
        {
            // Simple mapping or return as is
            return name;
        }

        private List<PriceData> GetFallbackPrices()
        {
            return new List<PriceData>
            {
                // Minerals
                new PriceData { MineralName = "Agricium", Price = "27.50 aUEC", NumericPrice = 27.50, BestLocation = "Mining Centers", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Aluminum", Price = "1.35 aUEC", NumericPrice = 1.35, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Astatine", Price = "10.00 aUEC", NumericPrice = 10.00, BestLocation = "Mining Centers", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Bexalite", Price = "40.00 aUEC", NumericPrice = 40.00, BestLocation = "Mining Centers", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Beryl", Price = "5.10 aUEC", NumericPrice = 5.10, BestLocation = "Mining Centers", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Borase", Price = "35.00 aUEC", NumericPrice = 35.00, BestLocation = "Mining Centers", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Copper", Price = "6.20 aUEC", NumericPrice = 6.20, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Corundum", Price = "2.90 aUEC", NumericPrice = 2.90, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Diamond", Price = "7.35 aUEC", NumericPrice = 7.35, BestLocation = "Trade Posts", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Fluorine", Price = "3.10 aUEC", NumericPrice = 3.10, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Gold", Price = "6.90 aUEC", NumericPrice = 6.90, BestLocation = "New Babbage", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Hephaestanite", Price = "16.00 aUEC", NumericPrice = 16.00, BestLocation = "Mining Centers", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Iodine", Price = "0.50 aUEC", NumericPrice = 0.50, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Iron", Price = "1.80 aUEC", NumericPrice = 1.80, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Laranite", Price = "32.50 aUEC", NumericPrice = 32.50, BestLocation = "ArcCorp Mining", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Quantanium", Price = "88.00 aUEC", NumericPrice = 88.00, BestLocation = "Refineries", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Quartz", Price = "1.65 aUEC", NumericPrice = 1.65, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Taranite", Price = "36.00 aUEC", NumericPrice = 36.00, BestLocation = "ArcCorp Mining", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Titanium", Price = "9.30 aUEC", NumericPrice = 9.30, BestLocation = "Trade Posts", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Tungsten", Price = "4.40 aUEC", NumericPrice = 4.40, BestLocation = "Trade Posts", Demand = "Low", StarSystem = "Stanton" },

                // Commodities / Hauling
                new PriceData { MineralName = "Agricultural Supplies", Price = "1.45 aUEC", NumericPrice = 1.45, BestLocation = "Farms", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Construction Materials", Price = "6,500 aUEC", NumericPrice = 6500, BestLocation = "Admin Centers", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Distilled Spirits", Price = "5.95 aUEC", NumericPrice = 5.95, BestLocation = "Bars", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Medical Supplies", Price = "19.80 aUEC", NumericPrice = 19.80, BestLocation = "Hospitals", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Processed Food", Price = "1.55 aUEC", NumericPrice = 1.55, BestLocation = "Cities", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "RMC", Price = "15,000 aUEC", NumericPrice = 15000, BestLocation = "TDD", Demand = "Very High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Scrap", Price = "1.75 aUEC", NumericPrice = 1.75, BestLocation = "Junkyards", Demand = "Low", StarSystem = "Stanton" },
                new PriceData { MineralName = "Stims", Price = "3.80 aUEC", NumericPrice = 3.80, BestLocation = "Pharmacies", Demand = "Medium", StarSystem = "Stanton" },
                new PriceData { MineralName = "Waste", Price = "0.05 aUEC", NumericPrice = 0.05, BestLocation = "Dump", Demand = "Low", StarSystem = "Stanton" },
                
                // Vice
                new PriceData { MineralName = "Widow", Price = "220.00 aUEC", NumericPrice = 220.00, BestLocation = "Hidden Terminals", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "E'tam", Price = "110.00 aUEC", NumericPrice = 110.00, BestLocation = "Hidden Terminals", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Neon", Price = "90.00 aUEC", NumericPrice = 90.00, BestLocation = "Hidden Terminals", Demand = "High", StarSystem = "Stanton" }
            };
        }
    }
}
