using System;
using System.Collections.Generic;
using System.Globalization;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Risk tier for attempting a fracture on a given rock.
    /// </summary>
    public enum FractureRisk
    {
        Low,
        Medium,
        High,
        Unsafe,
    }

    /// <summary>
    /// Solver output: recommended charge band, head, ship, and risk summary.
    /// </summary>
    public sealed record FractureRecommendation
    {
        /// <summary>Lower bound of the safe charge band, in % laser power.</summary>
        public required double ChargeLowerPct { get; init; }

        /// <summary>Upper bound of the safe charge band, in % laser power. Clamped to &lt;= 95.</summary>
        public required double ChargeUpperPct { get; init; }

        /// <summary>Recommended laser head (or "Pass" if the rock isn't worth charging in).</summary>
        public required string RecommendedHead { get; init; }

        /// <summary>Recommended ship / platform for this rock's mass bracket.</summary>
        public required string RecommendedShip { get; init; }

        /// <summary>Human-readable one-line advice.</summary>
        public required string Advice { get; init; }

        /// <summary>Fracture risk bucket based on instability + resistance.</summary>
        public required FractureRisk Risk { get; init; }
    }

    /// <summary>
    /// Pure-logic solver that maps a <see cref="RockScan"/> to a recommended fracture plan.
    /// No I/O, no hidden state — a single public <see cref="Solve"/> method.
    /// <para>
    /// Formulas are simplified approximations of community consensus from the Spectrum thread
    /// "mining calculator power resistance instability charge" and the R1 research doc
    /// (see tasks/research/R1-refinery-4.7.md).
    /// </para>
    /// </summary>
    public sealed class FractureSolver
    {
        // Precision-laser ore codes — high-value ores where the Helix II / Lancet heads' tighter
        // charge window pays for itself. Ore codes sourced from R1 section 1 (UEX canonical codes).
        private static readonly IReadOnlyCollection<string> PrecisionOreCodes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "QUAN", // Quantainium / Quantanium
                "TARA", // Taranite
                "BEXA", // Bexalite
                "LARA", // Laranite
                "HEPH", // Hephaestanite
            };

        private const double MinValuableCompositionForMiningPct = 30.0;
        private const double AbsoluteUpperChargeCap = 95.0;
        private const double AbsoluteLowerChargeFloor = 2.0;

        /// <summary>
        /// Compute a fracture recommendation for the given scan. Pure function; same input -> same output.
        /// </summary>
        public FractureRecommendation Solve(RockScan scan)
        {
            ArgumentNullException.ThrowIfNull(scan);

            double resistance = Clamp(scan.Resistance, 0.0, 100.0);
            double instability = Clamp(scan.Instability, 0.0, 100.0);
            double mass = scan.MassKg < 0.0 ? 0.0 : scan.MassKg;

            // Safe charge band — community consensus approximation:
            //   lower = 0.1 * Resistance + 2   (floor at 2%)
            //   upper = lower + 0.6 * (100 - Instability)   (cap at 95%)
            double lower = (0.1 * resistance) + 2.0;
            if (lower < AbsoluteLowerChargeFloor)
            {
                lower = AbsoluteLowerChargeFloor;
            }

            double upper = lower + (0.6 * (100.0 - instability));
            if (upper > AbsoluteUpperChargeCap)
            {
                upper = AbsoluteUpperChargeCap;
            }

            // Degenerate case: at extreme instability the band could collapse or invert.
            // Keep the invariant upper >= lower by clamping upper to lower.
            if (upper < lower)
            {
                upper = lower;
            }

            // Risk tier.
            FractureRisk risk = ClassifyRisk(instability, resistance);

            // Head recommendation.
            double valuablePct = scan.TotalValuableCompositionPct();
            double dominantPct = scan.DominantOrePct(out string dominantOre);
            string head;

            if (valuablePct < MinValuableCompositionForMiningPct)
            {
                head = "Pass — not worth the charge-in";
            }
            else if (!string.IsNullOrEmpty(dominantOre) && PrecisionOreCodes.Contains(dominantOre))
            {
                head = "Helix II / Lancet (precision)";
            }
            else
            {
                head = "FS-22 (bulk)";
            }

            // Ship recommendation by mass bracket. 4.7 adds dense deposits per R1 section 6.1
            // ("Refinery takes a bigger cut", "ore density reduced") so larger rocks increasingly
            // need multi-crew power.
            string ship;
            if (mass > 30000.0)
            {
                ship = "MOLE / multi-crew required (4.7 adds dense deposits requiring combined power)";
            }
            else if (mass > 18000.0)
            {
                ship = "MOLE preferred";
            }
            else if (mass < 1500.0)
            {
                ship = "ROC";
            }
            else
            {
                ship = "Prospector OK";
            }

            string advice = BuildAdvice(risk, head, lower, upper, valuablePct, dominantOre, dominantPct);

            return new FractureRecommendation
            {
                ChargeLowerPct = Math.Round(lower, 2),
                ChargeUpperPct = Math.Round(upper, 2),
                RecommendedHead = head,
                RecommendedShip = ship,
                Advice = advice,
                Risk = risk,
            };
        }

        private static FractureRisk ClassifyRisk(double instability, double resistance)
        {
            if (instability > 80.0 && resistance > 80.0)
            {
                return FractureRisk.Unsafe;
            }

            if (instability > 60.0 || resistance > 60.0)
            {
                return FractureRisk.High;
            }

            if (instability > 40.0 || resistance > 40.0)
            {
                return FractureRisk.Medium;
            }

            return FractureRisk.Low;
        }

        private static string BuildAdvice(
            FractureRisk risk,
            string head,
            double lower,
            double upper,
            double valuablePct,
            string dominantOre,
            double dominantPct)
        {
            if (risk == FractureRisk.Unsafe)
            {
                return "Do not attempt — instability and resistance both above 80%.";
            }

            if (head.StartsWith("Pass", StringComparison.OrdinalIgnoreCase))
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"Skip — only {valuablePct:0.#}% valuable composition; not worth charging in.");
            }

            string oreLabel = string.IsNullOrEmpty(dominantOre)
                ? "mixed"
                : string.Create(CultureInfo.InvariantCulture, $"{dominantOre} {dominantPct:0.#}%");

            return string.Create(
                CultureInfo.InvariantCulture,
                $"Charge {lower:0.#}-{upper:0.#}% with {head}; dominant ore {oreLabel}; risk {risk}.");
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
