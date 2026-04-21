using System;
using System.Collections.Generic;
using System.Linq;

namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// In-game scanner output for a single asteroid / rock. Pure data, deterministic helpers.
    /// Composition keys follow UEX commodity codes (e.g. QUAN, LARA, BEXA) as used across the rest
    /// of the app — see tasks/research/R1-refinery-4.7.md section 1 for the canonical code list.
    /// </summary>
    public sealed record RockScan
    {
        /// <summary>Rock mass in kilograms.</summary>
        public required double MassKg { get; init; }

        /// <summary>Instability, 0-100 percent.</summary>
        public required double Instability { get; init; }

        /// <summary>Resistance, 0-100 percent.</summary>
        public required double Resistance { get; init; }

        /// <summary>Current rock energy charge, 0-100 percent.</summary>
        public required double EnergyPct { get; init; }

        /// <summary>
        /// Ore-code -> percentage (0-100). Keys should be UEX codes (QUAN, LARA, BEXA, INERT, ...).
        /// Waste buckets are identified by the codes listed in <see cref="WasteOreCodes"/>.
        /// </summary>
        public required IReadOnlyDictionary<string, double> Composition { get; init; }

        /// <summary>UTC timestamp the scan was captured. Defaults to now on construction.</summary>
        public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Ore codes that carry no refinable / saleable value. INERT is the canonical waste code
        /// (see R1 section 6.2 — "Inert materials are always discarded").
        /// </summary>
        public static IReadOnlyCollection<string> WasteOreCodes { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INERT", "WASTE" };

        /// <summary>
        /// Sum of composition percentages for non-waste ores. Returns 0 for an empty composition.
        /// </summary>
        public double TotalValuableCompositionPct()
        {
            if (Composition is null || Composition.Count == 0)
            {
                return 0.0;
            }

            double total = 0.0;
            foreach (var kvp in Composition)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    continue;
                }

                if (WasteOreCodes.Contains(kvp.Key))
                {
                    continue;
                }

                total += kvp.Value;
            }

            // Clamp to [0, 100] to protect downstream math from bad scanner input.
            if (total < 0.0) total = 0.0;
            if (total > 100.0) total = 100.0;
            return total;
        }

        /// <summary>
        /// Highest-percentage single non-waste ore. Returns 0 and sets <paramref name="dominantOre"/>
        /// to an empty string when the composition is empty / all-waste.
        /// </summary>
        public double DominantOrePct(out string dominantOre)
        {
            dominantOre = string.Empty;
            if (Composition is null || Composition.Count == 0)
            {
                return 0.0;
            }

            double bestPct = 0.0;
            string bestKey = string.Empty;

            foreach (var kvp in Composition)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                {
                    continue;
                }

                if (WasteOreCodes.Contains(kvp.Key))
                {
                    continue;
                }

                if (kvp.Value > bestPct)
                {
                    bestPct = kvp.Value;
                    bestKey = kvp.Key;
                }
            }

            dominantOre = bestKey;
            return bestPct;
        }
    }
}
