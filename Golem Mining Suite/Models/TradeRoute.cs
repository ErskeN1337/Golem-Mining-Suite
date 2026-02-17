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
        
        // Formatted display properties
        public string BuyPriceFormatted => $"{BuyPrice:N2} aUEC";
        public string SellPriceFormatted => $"{SellPrice:N2} aUEC";
        public string ProfitPerSCUFormatted => $"{ProfitPerSCU:N2} aUEC/SCU";
        public string TotalProfitFormatted => $"{TotalProfit:N0} aUEC";
        public string RouteDescription => $"{BuyStation} → {SellStation}";
        public string SystemRoute => BuySystem == SellSystem 
            ? $"{BuySystem}" 
            : $"{BuySystem} → {SellSystem}";
    }
}
