namespace Golem_Mining_Suite.Models
{
    public class AsteroidMineralData
    {
        public required string MineralName { get; set; }
        public required string OreType { get; set; }
        public int Percentage { get; set; }

        /// <summary>
        /// Optional Star Citizen 4.7 quality score (0-1000) for this asteroid mineral instance.
        /// Null for catalog/reference rows with no scanned quality.
        /// </summary>
        public QualityScore? Quality { get; init; }
    }
}
