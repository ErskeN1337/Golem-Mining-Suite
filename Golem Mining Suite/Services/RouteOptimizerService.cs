using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Models.Piracy;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace Golem_Mining_Suite.Services
{
    public class RouteOptimizerService
    {
        private readonly IPiracyRouteAnalyzer? _piracyAnalyzer;

        /// <summary>
        /// Default ctor — no piracy integration. Preserves the original contract
        /// used by existing tests and consumers that never opt in to risk scoring.
        /// </summary>
        public RouteOptimizerService()
        {
        }

        /// <summary>
        /// DI-friendly ctor. <paramref name="piracyAnalyzer"/> is optional so the
        /// optimizer never hard-depends on Supabase-backed services.
        /// </summary>
        public RouteOptimizerService(IPiracyRouteAnalyzer? piracyAnalyzer)
        {
            _piracyAnalyzer = piracyAnalyzer;
        }

        /// <summary>
        /// Calculate profitable buy→sell trade routes.
        /// </summary>
        /// <param name="priceData">Terminal prices, one row per (commodity, terminal) pair.</param>
        /// <param name="cargoCapacity">Hold size in SCU.</param>
        /// <param name="maxBudget">aUEC cap on a single route's investment (0 = unlimited).</param>
        /// <param name="includePiracyRisk">
        /// When true and a piracy analyzer is wired, each route gets a
        /// <see cref="TradeRoute.RiskScore"/> computed from the direct buy→sell
        /// QT leg. Default false for back-compat.
        /// </param>
        public List<TradeRoute> CalculateRoutes(
            List<PriceData> priceData,
            int cargoCapacity,
            double maxBudget = 0,
            bool includePiracyRisk = false)
        {
            var routes = new List<TradeRoute>();

            // Group price data by commodity
            var commodityGroups = priceData.GroupBy(p => p.MineralName);

            foreach (var commodityGroup in commodityGroups)
            {
                var commodity = commodityGroup.Key;
                var prices = commodityGroup.ToList();

                // Find stations that sell this commodity (we can buy from them)
                // UnitSellPrice = what the station charges us to buy
                var buyStations = prices.Where(p => p.UnitSellPrice > 0).ToList();

                // Find stations that buy this commodity (we can sell to them)
                // UnitBuyPrice = what the station pays us to sell
                var sellStations = prices.Where(p => p.UnitBuyPrice > 0).ToList();

                if (buyStations.Count == 0 || sellStations.Count == 0)
                    continue;

                // Calculate profit for each buy→sell combination
                foreach (var buyStation in buyStations)
                {
                    foreach (var sellStation in sellStations)
                    {
                        // Skip if same station
                        if (buyStation.BestLocation == sellStation.BestLocation)
                            continue;

                        // Calculate profit per SCU
                        double profitPerSCU = sellStation.UnitBuyPrice - buyStation.UnitSellPrice;

                        // Only include profitable routes
                        if (profitPerSCU <= 0)
                            continue;

                        // Calculate how much we can actually buy
                        // 1. Limited by Cargo Capacity
                        int scuByCapacity = cargoCapacity;

                        // 2. Limited by Budget (if set)
                        int scuByBudget = int.MaxValue;
                        if (maxBudget > 0 && buyStation.UnitSellPrice > 0)
                        {
                            // Floor to nearest whole SCU
                            scuByBudget = (int)(maxBudget / buyStation.UnitSellPrice);
                        }

                        // Take the smaller limit
                        int scuTraded = System.Math.Min(scuByCapacity, scuByBudget);

                        if (scuTraded <= 0) continue; // Can't afford even 1 SCU

                        double totalProfit = profitPerSCU * scuTraded;
                        double investment = scuTraded * buyStation.UnitSellPrice;

                        routes.Add(new TradeRoute
                        {
                            CommodityName = commodity,
                            BuyStation = buyStation.BestLocation,
                            BuySystem = buyStation.StarSystem,
                            BuyPrice = buyStation.UnitSellPrice,
                            SellStation = sellStation.BestLocation,
                            SellSystem = sellStation.StarSystem,
                            SellPrice = sellStation.UnitBuyPrice,
                            ProfitPerSCU = profitPerSCU,
                            TotalProfit = totalProfit,
                            Demand = sellStation.Demand,
                            SCUTraded = scuTraded,
                            InvestmentCost = investment
                        });
                    }
                }
            }

            // Sort by total profit descending
            var sorted = routes.OrderByDescending(r => r.TotalProfit).ToList();

            // Optional piracy annotation. Fails closed — an analyzer exception never
            // breaks the main route pipeline.
            if (includePiracyRisk && _piracyAnalyzer != null)
            {
                foreach (var r in sorted)
                {
                    try
                    {
                        var leg = SynthesizeLeg(r);
                        var risk = _piracyAnalyzer.Analyze(new[] { leg });
                        r.Risk = risk;
                        r.RiskScore = risk.TotalRiskScore;
                    }
                    catch
                    {
                        // Analyzer failures must not poison the routes list.
                        r.RiskScore = null;
                        r.Risk = null;
                    }
                }
            }

            return sorted;
        }

        /// <summary>
        /// Build a placeholder QT leg for a trade route. We don't know canonical
        /// station coordinates (see R4); anchor both endpoints at (0,0,0) so the
        /// leg has zero length and the analyzer correctly returns zero hits
        /// rather than fabricating them. Real coords arrive via crowdsourcing and
        /// will be substituted here once the body/station table ships.
        /// </summary>
        private static QtLeg SynthesizeLeg(TradeRoute route)
        {
            return new QtLeg(
                FromName: $"{route.BuySystem}:{route.BuyStation}",
                From: new Vec3(0, 0, 0),
                ToName: $"{route.SellSystem}:{route.SellStation}",
                To: new Vec3(0, 0, 0));
        }

        public List<TradeRoute> FilterRoutes(List<TradeRoute> routes, string? systemFilter = null, double minProfit = 0)
        {
            var filtered = routes.AsEnumerable();

            // Filter by star system
            if (!string.IsNullOrEmpty(systemFilter) && systemFilter != "All")
            {
                filtered = filtered.Where(r =>
                    r.BuySystem.Contains(systemFilter) ||
                    r.SellSystem.Contains(systemFilter));
            }

            // Filter by minimum profit
            if (minProfit > 0)
            {
                filtered = filtered.Where(r => r.TotalProfit >= minProfit);
            }

            return filtered.ToList();
        }

        public List<TradeRoute> SortRoutes(List<TradeRoute> routes, string sortBy)
        {
            return sortBy switch
            {
                "TotalProfit" => routes.OrderByDescending(r => r.TotalProfit).ToList(),
                "ProfitPerSCU" => routes.OrderByDescending(r => r.ProfitPerSCU).ToList(),
                "Commodity" => routes.OrderBy(r => r.CommodityName).ToList(),
                "Demand" => routes.OrderByDescending(r => r.Demand == "High" ? 3 : r.Demand == "Medium" ? 2 : 1).ToList(),
                _ => routes
            };
        }
    }
}
