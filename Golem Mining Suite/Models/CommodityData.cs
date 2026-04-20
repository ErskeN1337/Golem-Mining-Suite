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

        /// <summary>
        /// Optional Star Citizen 4.7 quality score (0-1000) attached to this commodity instance.
        /// <c>null</c> for pre-4.7 data or commodities that do not carry a quality attribute
        /// (non-ore trade goods). See <see cref="QualityScore"/>.
        /// </summary>
        public QualityScore? Quality { get; init; }
    }
}
