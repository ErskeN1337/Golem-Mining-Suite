using Golem_Mining_Suite.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Service to parse OCR text from terminal screens into structured data
    /// </summary>
    public class TerminalParser
    {
        // Common commodity names to look for (minerals and gases only - for mining)
        private static readonly string[] KNOWN_MINERALS = new[]
        {
            // Refined minerals
            "Quantanium", "Bexalite", "Taranite", "Laranite", "Agricium",
            "Hephaestanite", "Beryl", "Gold", "Borase", "Tungsten",
            "Titanium", "Iron", "Quartz", "Copper", "Corundum", "Aluminum",
            "Diamond", "Hadanite", "Dolivine", "Aphorite", "Janalite", "Beradom", "Feynmaline",
            "Cobalt", "Atlasium", "Dymantium", "Riccite",
            
            // Gases
            "Hydrogen", "Nitrogen", "Methane", "Quantanium Gas", "Pressurized Ice"
        };

        /// <summary>
        /// Parse OCR text from a terminal screen
        /// </summary>
        public TerminalData? ParseTerminalText(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return null;

            var data = new TerminalData();
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "livedata_debug.log");

            // Extract commodity name
            data.CommodityName = ExtractCommodityName(ocrText);
            if (string.IsNullOrEmpty(data.CommodityName))
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] No commodity name found\n"); } catch { }
                return null; // No valid commodity found
            }

            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Found commodity: {data.CommodityName}\n"); } catch { }

            // Find the line(s) containing this commodity to extract prices from the correct context
            var commodityContext = ExtractCommodityContext(ocrText, data.CommodityName);
            
            // Extract prices from the commodity's context only
            data.PriceSell = ExtractPrice(commodityContext, "sell");
            data.PriceBuy = ExtractPrice(commodityContext, "buy");
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Prices - Sell: {data.PriceSell}, Buy: {data.PriceBuy}\n"); } catch { }

            // Extract inventory from the commodity's context
            var inventory = ExtractInventory(commodityContext);
            if (inventory.HasValue)
            {
                data.InventorySCU = inventory.Value.current;
                data.InventoryMax = inventory.Value.max;
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Inventory: {data.InventorySCU}/{data.InventoryMax}\n"); } catch { }
            }
            else
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] No inventory found\n"); } catch { }
            }

            // Extract terminal name (harder, might need improvement)
            data.TerminalName = ExtractTerminalName(ocrText);
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Terminal: {data.TerminalName}\n"); } catch { }

            // Validate before returning
            bool isValid = data.IsValid();
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] IsValid: {isValid}\n"); } catch { }
            
            if (!isValid)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Validation failed - Commodity: '{data.CommodityName}', Terminal: '{data.TerminalName}', PriceSell: {data.PriceSell}, PriceBuy: {data.PriceBuy}, Inv: {data.InventorySCU}/{data.InventoryMax}\n"); } catch { }
            }
            
            return isValid ? data : null;
        }

        private string ExtractCommodityContext(string text, string commodityName)
        {
            // Find lines containing the commodity name and surrounding context
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var contextLines = new List<string>();
            
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(commodityName, StringComparison.OrdinalIgnoreCase))
                {
                    // Include the commodity line and the next 3 lines (for price, inventory, etc.)
                    contextLines.Add(lines[i]);
                    for (int j = 1; j <= 3 && i + j < lines.Length; j++)
                    {
                        contextLines.Add(lines[i + j]);
                    }
                    break; // Only process the first match
                }
            }
            
            return string.Join("\n", contextLines);
        }

        private string ExtractCommodityName(string text)
        {
            // Look for known mineral names in the text
            foreach (var mineral in KNOWN_MINERALS)
            {
                if (text.Contains(mineral, StringComparison.OrdinalIgnoreCase))
                {
                    return mineral;
                }
            }

            return string.Empty;
        }

        private int ExtractPrice(string text, string priceType)
        {
            // Star Citizen terminals show prices like:
            // "H2.58599996K/UNITS" or "£11.06200003K/SCU" or "128.9160003K/5C" (OCR typo)
            // OCR sometimes reads decimal points as spaces: "1128 9160003K/SC" instead of "1128.9160003K/SC"
            
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "livedata_debug.log");
            
            // First, normalize spaces that might be decimal points in price numbers
            // Replace space between digits followed by K/ with a decimal point
            text = Regex.Replace(text, @"(\d+)\s+(\d+)\s*K/", "$1.$2K/", RegexOptions.IgnoreCase);
            
            // Look for price patterns - be flexible with OCR errors
            // IMPORTANT: Order matters! Check K/ patterns FIRST before currency-only patterns
            var pricePatterns = new[]
            {
                // K/ patterns (must be first to avoid matching just the currency symbol)
                // OCR often reads SCU as: 5C, 5CU, SCI, ¢ (cent symbol), § (section symbol)
                @"[H£r€$]\s*(\d+(?:\.\d+)?)\s*K/(?:SCU|UNITS|SC|SCI|5C|5CU|UNIT|¢|§|s)",  // With currency + K/
                @"(\d+(?:\.\d+)?)\s*K/(?:SCU|UNITS|SC|UNIT|5C|5CU|SCI|¢|§|s)",            // Just number + K/
                
                // Non-K patterns
                @"[H£r€$]?\s*(\d+(?:\.\d+)?)\s*/(?:SCU|UNITS)",                           // Without K
                @"(\d{1,3}(?:,\d{3})*)\s*(?:aUEC|UEC)",                                   // 88,000 aUEC
                @"(?:sell|price)[\s:]+(\d{1,3}(?:,\d{3})*)",                              // Sell: 88,000
                @"[£r€$]\s*(\d+(?:\.\d+)?)"                                               // Just currency + number (LAST!)
            };

            foreach (var pattern in pricePatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string priceStr = match.Groups[1].Value.Replace(",", "");
                    if (double.TryParse(priceStr, out double price))
                    {
                        // If the pattern includes K, multiply by 1000
                        if (match.Value.Contains("K/", StringComparison.OrdinalIgnoreCase) || 
                            match.Value.Contains("K ", StringComparison.OrdinalIgnoreCase))
                        {
                            price *= 1000;
                        }
                        
                        // Convert to integer (aUEC)
                        int result = (int)Math.Round(price);
                        try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Price matched: '{match.Value}' -> {result} aUEC\n"); } catch { }
                        return result;
                    }
                }
            }

            return 0;
        }

        private (int current, int max)? ExtractInventory(string text)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "livedata_debug.log");
            
            // Star Citizen terminals show inventory in various formats:
            // "HYDROGEN 0 scu" (current inventory)
            // "MAX INVENTORY 18,000SCU" or "18,000 SCU" (max capacity)
            // Sometimes on same line, sometimes separate lines
            
            // Try to find current inventory (the number before "scu" on the commodity line)
            var currentMatch = Regex.Match(text, @"(\d+)\s*(?:scu|&80|sc0|s)", RegexOptions.IgnoreCase);
            
            // Try to find max inventory
            var maxMatch = Regex.Match(text, @"(?:MAX\s*INVENTORY|INVENTORY)\s*(\d{1,3}(?:,\d{3})*)\s*(?:SCU|sc0|s)", RegexOptions.IgnoreCase);
            
            int? current = null;
            int? max = null;
            
            if (currentMatch.Success && int.TryParse(currentMatch.Groups[1].Value.Replace(",", ""), out int curr))
            {
                current = curr;
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Inventory current matched: '{currentMatch.Value}' -> {curr}\n"); } catch { }
            }
            
            if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value.Replace(",", ""), out int mx))
            {
                max = mx;
                try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Inventory max matched: '{maxMatch.Value}' -> {mx}\n"); } catch { }
            }
            
            // If we found both, return them
            if (current.HasValue && max.HasValue)
            {
                return (current.Value, max.Value);
            }
            
            // Legacy pattern: "1234/5000 SCU" or "1,234 / 5,000"
            var legacyMatch = Regex.Match(text, @"(\d{1,3}(?:,\d{3})*)\s*/\s*(\d{1,3}(?:,\d{3})*)\s*(?:SCU)?", RegexOptions.IgnoreCase);
            if (legacyMatch.Success)
            {
                string currentStr = legacyMatch.Groups[1].Value.Replace(",", "");
                string maxStr = legacyMatch.Groups[2].Value.Replace(",", "");

                if (int.TryParse(currentStr, out int legacyCurr) && int.TryParse(maxStr, out int legacyMax))
                {
                    try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Inventory legacy matched: '{legacyMatch.Value}' -> {legacyCurr}/{legacyMax}\n"); } catch { }
                    return (legacyCurr, legacyMax);
                }
            }

            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Parser] Inventory extraction failed. Text: '{text.Substring(0, Math.Min(100, text.Length))}...'\n"); } catch { }
            return null;
        }

        private string ExtractTerminalName(string text)
        {
            // This is tricky - terminal names vary widely
            // Common patterns: "Port Olisar", "Area 18", "Lorville"
            // For now, return a placeholder - we'll improve this with testing
            
            // Look for common terminal keywords
            var terminalKeywords = new[] { "Port", "Area", "Station", "Terminal", "Lorville", "Orison", "New Babbage" };
            
            foreach (var keyword in terminalKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the line containing the keyword
                    var lines = text.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        {
                            return line.Trim();
                        }
                    }
                }
            }

            return "Unknown Terminal";
        }
    }
}
