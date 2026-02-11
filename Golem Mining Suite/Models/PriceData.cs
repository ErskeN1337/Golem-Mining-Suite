namespace Golem_Mining_Suite.Models
{
    public class PriceData
    {
        public required string MineralName { get; set; }
        public required string Price { get; set; }
        public double NumericPrice { get; set; } // Added for easier sorting
        public required string BestLocation { get; set; }
        public required string Demand { get; set; }
        public required string StarSystem { get; set; }
        public string? LastUpdatedText { get; set; }
        public System.DateTime? LastUpdated { get; set; }
    }
}