namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// Represents parsed data from a Star Citizen commodity terminal
    /// </summary>
    public class TerminalData
    {
        public string CommodityName { get; set; } = string.Empty;
        public string TerminalName { get; set; } = string.Empty;
        public string StarSystem { get; set; } = string.Empty;
        public int PriceBuy { get; set; }
        public int PriceSell { get; set; }
        public int InventorySCU { get; set; }
        public int InventoryMax { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Calculate demand based on inventory levels
        /// </summary>
        public string GetDemand()
        {
            if (InventoryMax == 0) return "Unknown";
            double inventoryPercent = (double)InventorySCU / InventoryMax * 100;
            return inventoryPercent < 50 ? "High" : "Low";
        }
        
        /// <summary>
        /// Validate that the data is reasonable
        /// </summary>
        public bool IsValid(bool ignoreTerminalName = false)
        {
            // Basic validation
            if (string.IsNullOrEmpty(CommodityName)) return false;
            
            // Allow ignoring terminal name check for the "Prompt User" phase
            if (!ignoreTerminalName && (string.IsNullOrEmpty(TerminalName) || TerminalName == "Unknown Terminal")) return false;
            
            // Allow PriceSell to be 0 (OUT OF STOCK), but must have at least one price
            if (PriceSell == 0 && PriceBuy == 0) return false;
            
            // Inventory validation (optional, but good to have sane values)
            if (InventorySCU < 0 || InventoryMax < 0) return false;

            return true;
        }
    }
}
