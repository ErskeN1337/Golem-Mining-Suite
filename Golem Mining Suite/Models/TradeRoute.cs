using Golem_Mining_Suite.Models.Piracy;

namespace Golem_Mining_Suite.Models
{
    public class TradeRoute
    {
        public required string CommodityName { get; set; }
        public required string BuyStation { get; set; }
        public required string BuySystem { get; set; }
        public double BuyPrice { get; set; }
        public required string SellStation { get; set; }
        public required string SellSystem { get; set; }
        public double SellPrice { get; set; }
        public double ProfitPerSCU { get; set; }
        public double TotalProfit { get; set; }
        public required string Demand { get; set; }

        // New properties for Budget feature
        public int SCUTraded { get; set; }
        public double InvestmentCost { get; set; }

        /// <summary>
        /// Normalised 0..100 piracy risk score, populated only when the
        /// RouteOptimizerService was called with <c>IncludePiracyRisk = true</c>.
        /// Null otherwise so existing consumers are unaffected.
        /// </summary>
        public double? RiskScore { get; set; }

        /// <summary>
        /// Full piracy analysis (legs + pull-point hits + summary), populated
        /// alongside <see cref="RiskScore"/>. Null when piracy analysis did not
        /// run — callers should treat the two properties as linked.
        /// </summary>
        public RouteRisk? Risk { get; set; }

        // Formatted display properties
        public string BuyPriceFormatted => $"{BuyPrice:N2} aUEC";
        public string SellPriceFormatted => $"{SellPrice:N2} aUEC";
        public string ProfitPerSCUFormatted => $"{ProfitPerSCU:N2} aUEC/SCU";
        public string TotalProfitFormatted => $"{TotalProfit:N0} aUEC";
        public string InvestmentFormatted => $"{InvestmentCost:N0} aUEC";
        public string SCUTradedFormatted => $"{SCUTraded} SCU";
        public string RouteDescription => $"{BuyStation} → {SellStation}";
        public string SystemRoute => BuySystem == SellSystem
            ? $"{BuySystem}"
            : $"{BuySystem} → {SellSystem}";

        /// <summary>
        /// Short display string for the risk column — "—" when not analysed,
        /// the integer score otherwise.
        /// </summary>
        public string RiskScoreFormatted => RiskScore.HasValue
            ? ((int)System.Math.Round(RiskScore.Value)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "—";
    }
}
