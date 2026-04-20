using FluentAssertions;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Pre-4.7 baseline tests for RouteOptimizerService. The service is pure-logic (no I/O),
    /// so we feed it handcrafted PriceData lists and assert the routes it produces.
    /// </summary>
    public class RouteOptimizerServiceTests
    {
        private static PriceData MakePrice(
            string mineral,
            string location,
            string system,
            double unitSellPrice,
            double unitBuyPrice,
            string demand = "Medium")
        {
            return new PriceData
            {
                MineralName = mineral,
                Price = unitBuyPrice.ToString("F0"),
                NumericPrice = unitBuyPrice,
                UnitSellPrice = unitSellPrice,
                UnitBuyPrice = unitBuyPrice,
                BestLocation = location,
                StarSystem = system,
                Demand = demand
            };
        }

        [Fact]
        public void CalculateRoutes_EmptyInput_ReturnsEmptyListNotNull()
        {
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(new List<PriceData>(), cargoCapacity: 100);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void CalculateRoutes_SingleTerminal_ProducesNoRoutes()
        {
            // One station can't form a buy→sell pair with itself — code guards against same-location trades.
            var prices = new List<PriceData>
            {
                MakePrice("Quantanium", "ARCL1", "Stanton", unitSellPrice: 80000, unitBuyPrice: 88000)
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100);

            result.Should().BeEmpty("a single terminal cannot form any inter-terminal routes");
        }

        [Fact]
        public void CalculateRoutes_TwoTerminals_ReturnsExpectedTopRoute()
        {
            // Synthetic scenario:
            //   ARCL1 sells Quantanium at 80,000/SCU  (terminal CHARGES us 80k)
            //   BA18  buys  Quantanium at 88,000/SCU  (terminal PAYS us 88k)
            //   Profit/SCU = 8,000. Cargo = 100 SCU. Expected total = 800,000.
            var prices = new List<PriceData>
            {
                MakePrice("Quantanium", "ARCL1", "Stanton", unitSellPrice: 80000, unitBuyPrice: 0),
                MakePrice("Quantanium", "BA18",  "Stanton", unitSellPrice: 0,     unitBuyPrice: 88000, demand: "High")
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100);

            result.Should().ContainSingle();
            var route = result[0];
            route.CommodityName.Should().Be("Quantanium");
            route.BuyStation.Should().Be("ARCL1");
            route.SellStation.Should().Be("BA18");
            route.BuyPrice.Should().Be(80000);
            route.SellPrice.Should().Be(88000);
            route.ProfitPerSCU.Should().Be(8000);
            route.SCUTraded.Should().Be(100);
            route.TotalProfit.Should().Be(800_000);
            route.InvestmentCost.Should().Be(8_000_000);
            route.Demand.Should().Be("High");
        }

        [Fact]
        public void CalculateRoutes_UnprofitablePairs_AreExcluded()
        {
            // Buy high, sell low → negative profit → no route.
            var prices = new List<PriceData>
            {
                MakePrice("Iron", "ARCL1", "Stanton", unitSellPrice: 3000, unitBuyPrice: 0),
                MakePrice("Iron", "BA18",  "Stanton", unitSellPrice: 0,    unitBuyPrice: 2400)
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100);

            result.Should().BeEmpty("negative-profit pairings must not be emitted as routes");
        }

        [Fact]
        public void CalculateRoutes_OrdersResultsByTotalProfitDescending()
        {
            // Two commodities, two routes. The higher-total-profit route must come first.
            var prices = new List<PriceData>
            {
                // Low-margin route: Iron 2400→2500 = 100/SCU * 100 = 10,000
                MakePrice("Iron",       "BUY-A",  "Stanton", unitSellPrice: 2400,  unitBuyPrice: 0),
                MakePrice("Iron",       "SELL-A", "Stanton", unitSellPrice: 0,     unitBuyPrice: 2500),
                // High-margin route: Quantanium 80000→88000 = 8000/SCU * 100 = 800,000
                MakePrice("Quantanium", "BUY-Q",  "Stanton", unitSellPrice: 80000, unitBuyPrice: 0),
                MakePrice("Quantanium", "SELL-Q", "Stanton", unitSellPrice: 0,     unitBuyPrice: 88000),
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100);

            result.Should().HaveCount(2);
            result[0].CommodityName.Should().Be("Quantanium");
            result[0].TotalProfit.Should().BeGreaterThan(result[1].TotalProfit);
        }

        [Fact]
        public void CalculateRoutes_BudgetConstraint_ClampsScuTraded()
        {
            // Cargo allows 100 SCU, but budget of 400,000 aUEC at 80k/SCU only allows 5 SCU.
            var prices = new List<PriceData>
            {
                MakePrice("Quantanium", "ARCL1", "Stanton", unitSellPrice: 80000, unitBuyPrice: 0),
                MakePrice("Quantanium", "BA18",  "Stanton", unitSellPrice: 0,     unitBuyPrice: 88000)
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100, maxBudget: 400_000);

            result.Should().ContainSingle();
            result[0].SCUTraded.Should().Be(5, "budget divided by buy price should floor to 5 SCU");
            result[0].TotalProfit.Should().Be(40_000, "5 SCU × 8,000 aUEC/SCU");
        }

        [Fact]
        public void CalculateRoutes_BudgetBelowOneScu_SkipsRoute()
        {
            // Budget is less than even a single SCU costs → skip route entirely.
            var prices = new List<PriceData>
            {
                MakePrice("Quantanium", "ARCL1", "Stanton", unitSellPrice: 80000, unitBuyPrice: 0),
                MakePrice("Quantanium", "BA18",  "Stanton", unitSellPrice: 0,     unitBuyPrice: 88000)
            };
            var sut = new RouteOptimizerService();

            var result = sut.CalculateRoutes(prices, cargoCapacity: 100, maxBudget: 1000);

            result.Should().BeEmpty("cannot afford even 1 SCU at 80k with a 1k budget");
        }

        [Fact]
        public void FilterRoutes_BySystem_RetainsOnlyMatchingRoutes()
        {
            var routes = new List<TradeRoute>
            {
                new TradeRoute { CommodityName = "A", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", TotalProfit = 100 },
                new TradeRoute { CommodityName = "B", BuyStation = "X", BuySystem = "Pyro",    SellStation = "Y", SellSystem = "Pyro",    Demand = "Medium", TotalProfit = 200 },
            };
            var sut = new RouteOptimizerService();

            var filtered = sut.FilterRoutes(routes, systemFilter: "Pyro");

            filtered.Should().ContainSingle();
            filtered[0].CommodityName.Should().Be("B");
        }

        [Fact]
        public void FilterRoutes_MinProfit_ExcludesLowRoutes()
        {
            var routes = new List<TradeRoute>
            {
                new TradeRoute { CommodityName = "A", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", TotalProfit = 50 },
                new TradeRoute { CommodityName = "B", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", TotalProfit = 500 },
            };
            var sut = new RouteOptimizerService();

            var filtered = sut.FilterRoutes(routes, minProfit: 100);

            filtered.Should().ContainSingle();
            filtered[0].CommodityName.Should().Be("B");
        }

        [Fact]
        public void SortRoutes_ByProfitPerScu_OrdersDescending()
        {
            var routes = new List<TradeRoute>
            {
                new TradeRoute { CommodityName = "A", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", ProfitPerSCU = 100 },
                new TradeRoute { CommodityName = "B", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", ProfitPerSCU = 500 },
                new TradeRoute { CommodityName = "C", BuyStation = "X", BuySystem = "Stanton", SellStation = "Y", SellSystem = "Stanton", Demand = "Medium", ProfitPerSCU = 300 },
            };
            var sut = new RouteOptimizerService();

            var sorted = sut.SortRoutes(routes, "ProfitPerSCU");

            sorted.Select(r => r.ProfitPerSCU).Should().ContainInOrder(500, 300, 100);
        }
    }
}
