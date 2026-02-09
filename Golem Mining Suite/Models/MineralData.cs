using System.Collections.Generic;

namespace Golem_Mining_Suite.Models
{
    public class MineralData
    {
        public required string MineralName { get; set; }
        public System.Collections.Generic.List<OreSource> OreSources { get; set; } = new();
    }

    public class OreSource
    {
        public required string OreName { get; set; }
        public double Percentage { get; set; }
    }
}
