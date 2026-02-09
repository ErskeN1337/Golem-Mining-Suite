using Golem_Mining_Suite.Models;
using System;
using System.Text.RegularExpressions;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Service to parse OCR text from terminal screens into structured data
    /// </summary>
    public class TerminalParser
    {
        // Common commodity names to look for (minerals, gases, agricultural products)
        private static readonly string[] KNOWN_MINERALS = new[]
        {
            // Refined minerals
            "Quantanium", "Bexalite", "Taranite", "Laranite", "Agricium",
            "Hephaestanite", "Beryl", "Gold", "Borase", "Tungsten",
            "Titanium", "Iron", "Quartz", "Copper", "Corundum", "Aluminum",
            "Diamond", "Hadanite", "Dolivine", "Aphorite", "Janalite", "Beradom", "Feynmaline",
            
            // Agricultural/Organic
            "Revenant Tree Pollen", "Maze", "Wheat", "Stims", "Medical Supplies",
            
            // Gases
            "Hydrogen", "Quantanium Gas", "Pressurized Ice"
        };

        /// <summary>
        /// Parse OCR text from a terminal screen
        /// </summary>
        public TerminalData? ParseTerminalText(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return null;

            var data = new TerminalData();

            // Extract commodity name
            data.CommodityName = ExtractCommodityName(ocrText);
            if (string.IsNullOrEmpty(data.CommodityName))
                return null; // No valid commodity found

            // Extract prices
            data.PriceSell = ExtractPrice(ocrText, "sell");
            data.PriceBuy = ExtractPrice(ocrText, "buy");

            // Extract inventory
            var inventory = ExtractInventory(ocrText);
            if (inventory.HasValue)
            {
                data.InventorySCU = inventory.Value.current;
                data.InventoryMax = inventory.Value.max;
            }

            // Extract terminal name (harder, might need improvement)
            data.TerminalName = ExtractTerminalName(ocrText);

            // Validate before returning
            return data.IsValid() ? data : null;
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
            // "£11.06200003K/SCU" or "r12.00900006K/SCU" or "OUT OF STOCK"
            
            // Look for price patterns with currency symbols and /SCU or K/SCU suffix
            // Match patterns like: £11.06, r12.00, 88.50K/SCU, etc.
            var pricePatterns = new[]
            {
                @"[£r€$]?\s*(\d+(?:\.\d+)?)\s*(?:K)?/SCU",  // £11.06/SCU or 88K/SCU
                @"[£r€$]\s*(\d+(?:\.\d+)?)",                 // £11.06 or r12.00
                @"(\d{1,3}(?:,\d{3})*)\s*(?:aUEC|UEC)",     // 88,000 aUEC
                @"(?:sell|price)[\s:]+(\d{1,3}(?:,\d{3})*)" // Sell: 88,000 (fallback)
            };

            foreach (var pattern in pricePatterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string priceStr = match.Groups[1].Value.Replace(",", "").Replace("K", "000");
                    if (double.TryParse(priceStr, out double price))
                    {
                        // Convert to integer (aUEC)
                        return (int)Math.Round(price);
                    }
                }
            }

            return 0;
        }

        private (int current, int max)? ExtractInventory(string text)
        {
            // Look for patterns like "Stock: 1,234 / 5,000 SCU" or "1234/5000"
            var match = Regex.Match(text, @"(\d{1,3}(?:,\d{3})*)\s*/\s*(\d{1,3}(?:,\d{3})*)\s*(?:SCU)?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string currentStr = match.Groups[1].Value.Replace(",", "");
                string maxStr = match.Groups[2].Value.Replace(",", "");

                if (int.TryParse(currentStr, out int current) && int.TryParse(maxStr, out int max))
                {
                    return (current, max);
                }
            }

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
