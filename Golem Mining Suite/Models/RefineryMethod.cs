namespace Golem_Mining_Suite.Models
{
    public class RefineryMethod
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public double YieldBonus { get; set; }
        public double CostPercent { get; set; }
        public int YieldRating { get; set; }
        public int CostRating { get; set; }
        public int SpeedRating { get; set; }
    }
}
