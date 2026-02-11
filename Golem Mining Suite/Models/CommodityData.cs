namespace Golem_Mining_Suite.Models
{
    public class CommodityData
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // e.g. "agricultural_supplies"
        public string Type { get; set; } = "Commodity";
        public double AveragePriceBuy { get; set; }
        public double AveragePriceSell { get; set; }
        public bool IsHighlighted { get; set; }
    }
}
