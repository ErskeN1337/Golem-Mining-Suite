namespace Golem_Mining_Suite.Models
{
    public class PriceData
    {
        public required string MineralName { get; set; }
        public required string Price { get; set; }
        public double NumericPrice { get; set; } // Legacy/Sort (defaults to SellPrice or BuyPrice depending on context)
        public double UnitBuyPrice { get; set; } // Terminal PAYS this (We Sell)
        public double UnitSellPrice { get; set; } // Terminal CHARGES this (We Buy)
        public required string BestLocation { get; set; }
        public required string Demand { get; set; }
        public required string StarSystem { get; set; }
        public string? LastUpdatedText { get; set; }
        public System.DateTime? LastUpdated { get; set; }
    }
}