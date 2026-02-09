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
                        string starSystem = terminal.GetProperty("star_system_name").GetString();
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
                        var priceSell = priceEntry.GetProperty("price_sell").GetInt32();

                        int terminalId = priceEntry.GetProperty("id_terminal").GetInt32();
                        string starSystem = _terminalToSystem.ContainsKey(terminalId) ? _terminalToSystem[terminalId] : "Unknown";

                        // Skip checking SCU for simplicity or implement if needed
                        int scu = 0;
                        int scuMax = 100;

                        if (priceEntry.TryGetProperty("scu", out JsonElement scuElement))
                        {
                            scu = scuElement.ValueKind == JsonValueKind.Number ? scuElement.GetInt32() : 0;
                        }

                        if (priceEntry.TryGetProperty("scu_max", out JsonElement scuMaxElement))
                        {
                            scuMax = scuMaxElement.ValueKind == JsonValueKind.Number ? scuMaxElement.GetInt32() : 0;
                        }

                        if (priceSell <= 0)
                            continue;

                        var displayName = MapCommodityName(commodityName);

                        if (IsMineralName(displayName))
                        {
                            double inventoryPercent = scuMax > 0 ? (double)scu / scuMax * 100 : 0;
                            string demand = inventoryPercent < 50 ? "High" : "Low";

                            // Check for LIVE override
                            var overrideData = _liveOverrides.FirstOrDefault(o => o.MineralName == displayName && o.BestLocation == terminalName);
                            if (overrideData != null)
                            {
                                // Use live data
                                priceList.Add(overrideData);
                            }
                            else
                            {
                                priceList.Add(new PriceData
                                {
                                    MineralName = displayName,
                                    Price = $"{priceSell:N0} aUEC",
                                    NumericPrice = priceSell,
                                    BestLocation = terminalName,
                                    Demand = demand,
                                    StarSystem = starSystem,
                                    LastUpdatedText = "API" // Explicitly mark as API
                                });
                            }
                        }
                    }
                }
                
                // Also add any live overrides that weren't in the API list (new entries?)
                // Effectively merging:
                foreach (var live in _liveOverrides)
                {
                    if (!priceList.Contains(live))
                    {
                        priceList.Add(live);
                    }
                }
            }
            catch (Exception)
            {
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
        {
            var minerals = new HashSet<string>
            {
                "Quantanium", "Bexalite", "Taranite", "Laranite", "Agricium",
                "Hephaestanite", "Beryl", "Gold", "Borase", "Tungsten",
                "Titanium", "Iron", "Quartz", "Copper", "Corundum", "Aluminum"
            };

            return minerals.Contains(name);
        }

        private List<PriceData> GetFallbackPrices()
        {
            return new List<PriceData>
            {
                new PriceData { MineralName = "Quantanium", Price = "88,000 aUEC", NumericPrice = 88000, BestLocation = "Area 18", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Bexalite", Price = "40,000 aUEC", NumericPrice = 40000, BestLocation = "Lorville", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Taranite", Price = "36,000 aUEC", NumericPrice = 36000, BestLocation = "Orison", Demand = "High", StarSystem = "Stanton" },
                new PriceData { MineralName = "Gold", Price = "6,000 aUEC", NumericPrice = 6000, BestLocation = "New Babbage", Demand = "Low", StarSystem = "Stanton" }
            };
        }
    }
}
