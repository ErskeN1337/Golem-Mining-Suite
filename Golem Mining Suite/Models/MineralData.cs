using System.Collections.Generic;

namespace Golem_Mining_Suite.Models
{
    public class MineralData
    {
        public required string MineralName { get; set; }
        public required List<OreSource> OreSources { get; set; }
    }

    public class OreSource
    {
        public required string OreName { get; set; }
        public double Percentage { get; set; }
    }
}
