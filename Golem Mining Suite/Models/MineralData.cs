using System.Collections.Generic;

namespace Golem_Mining_Suite.Models
{
    public class MineralData
    {
        public required string MineralName { get; set; }
        public System.Collections.Generic.List<OreSource> OreSources { get; set; } = new();

        /// <summary>
        /// Optional Star Citizen 4.7 quality score (0-1000) for this mineral instance. Null when
        /// the source has no quality info (pre-4.7 data, catalog entries, etc.).
        /// </summary>
        public QualityScore? Quality { get; init; }
    }

    public class OreSource
    {
        public required string OreName { get; set; }
        public double Percentage { get; set; }
    }
}
