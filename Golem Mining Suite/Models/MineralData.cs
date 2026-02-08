using System.Collections.Generic;

namespace Golem_Mining_Suite.Models
{
    public class MineralData
    {
        public string MineralName { get; set; }
        public List<OreSource> OreSources { get; set; }
    }

    public class OreSource
    {
        public string OreName { get; set; }
        public double Percentage { get; set; }
    }
}
