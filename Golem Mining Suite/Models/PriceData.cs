namespace Golem_Mining_Suite.Models
{
    public class PriceData
    {
        public string MineralName { get; set; }
        public string Price { get; set; }
        public double NumericPrice { get; set; } // Added for easier sorting
        public string BestLocation { get; set; }
        public string Demand { get; set; }
        public string StarSystem { get; set; }
    }
}