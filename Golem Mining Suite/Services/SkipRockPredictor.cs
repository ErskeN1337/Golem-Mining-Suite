using System;
using System.Collections.Generic;
using System.Globalization;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Heuristic result: skip-or-mine probability score plus a one-line reasoning string.
    /// </summary>
    public sealed record SkipDecision
    {
        /// <summary>Probability the rock is worth mining, in [0, 1].</summary>
        public required double ProfitProbability { get; init; }

        /// <summary>One-line human-readable explanation.</summary>
        public required string Reasoning { get; init; }

        /// <summary>True when <see cref="ProfitProbability"/> &lt; 0.35.</summary>
        public required bool RecommendSkip { get; init; }
    }

    /// <summary>
    /// Pure-logic predictor that turns a <see cref="RockScan"/> (+ optional prices) into a
    /// go/no-go recommendation. Simple multiplicative heuristic, no ML.
    /// </summary>
    public sealed class SkipRockPredictor
    {
        private const double BaselineProbability = 0.5;
        private const double SkipThreshold = 0.35;

        // "Good price" reference used when callers supply an orePrices map. 10,000 aUEC / cSCU is
        // the rule-of-thumb benchmark for a solidly profitable ore in current community guides.
        private const decimal ReferenceGoodPriceAuecPerCscu = 10_000m;

        private readonly FractureSolver _fractureSolver;

        public SkipRockPredictor()
            : this(new FractureSolver())
        {
        }

        /// <summary>
        /// Overload for tests that want to inject a solver (still pure — same in, same out).
        /// </summary>
        public SkipRockPredictor(FractureSolver fractureSolver)
        {
            ArgumentNullException.ThrowIfNull(fractureSolver);
            _fractureSolver = fractureSolver;
        }

        /// <summary>
        /// Evaluate a scan. <paramref name="orePrices"/> keys should be UEX commodity codes matching
        /// <see cref="RockScan.Composition"/>. Missing / null prices are ignored (no price factor).
        /// </summary>
        public SkipDecision Evaluate(RockScan scan, IReadOnlyDictionary<string, decimal>? orePrices = null)
        {
            ArgumentNullException.ThrowIfNull(scan);

            double probability = BaselineProbability;

            double valuablePct = scan.TotalValuableCompositionPct();
            double dominantPct = scan.DominantOrePct(out string dominantOre);

            // Composition factor: waste drags the score down. valuablePct is already clamped to [0, 100].
            probability *= valuablePct / 100.0;

            // Risk factor.
            FractureRecommendation rec = _fractureSolver.Solve(scan);
            double riskFactor = rec.Risk switch
            {
                FractureRisk.Unsafe => 0.3,
                FractureRisk.High => 0.6,
                _ => 1.0,
            };
            probability *= riskFactor;

            // Optional price factor — only applied when prices provided AND we have a dominant ore.
            bool priceFactorApplied = false;
            decimal dominantPrice = 0m;
            if (orePrices is not null
                && !string.IsNullOrEmpty(dominantOre)
                && orePrices.TryGetValue(dominantOre, out decimal priceLookup)
                && priceLookup > 0m)
            {
                dominantPrice = priceLookup;
                double ratio = (double)(dominantPrice / ReferenceGoodPriceAuecPerCscu);
                if (ratio > 1.0) ratio = 1.0;
                if (ratio < 0.0) ratio = 0.0;
                probability *= ratio;
                priceFactorApplied = true;
            }

            // Clamp.
            if (probability < 0.0) probability = 0.0;
            if (probability > 1.0) probability = 1.0;

            bool recommendSkip = probability < SkipThreshold;
            string reasoning = BuildReasoning(
                scan,
                valuablePct,
                dominantOre,
                dominantPct,
                rec.Risk,
                priceFactorApplied,
                dominantPrice,
                probability,
                recommendSkip);

            return new SkipDecision
            {
                ProfitProbability = Math.Round(probability, 4),
                Reasoning = reasoning,
                RecommendSkip = recommendSkip,
            };
        }

        private static string BuildReasoning(
            RockScan scan,
            double valuablePct,
            string dominantOre,
            double dominantPct,
            FractureRisk risk,
            bool priceFactorApplied,
            decimal dominantPrice,
            double probability,
            bool recommendSkip)
        {
            var parts = new List<string>(capacity: 4);

            parts.Add(string.Create(CultureInfo.InvariantCulture,
                $"valuable content {valuablePct:0.#}%"));

            if (!string.IsNullOrEmpty(dominantOre))
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"dominant {dominantOre} ({dominantPct:0.#}%)"));
            }

            if (risk >= FractureRisk.High)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{risk.ToString().ToLowerInvariant()} fracture risk (instability {scan.Instability:0.#}%, resistance {scan.Resistance:0.#}%)"));
            }

            if (priceFactorApplied)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture,
                    $"price {dominantPrice:0} aUEC/cSCU"));
            }

            string verdict = recommendSkip ? "skip" : "mine";
            string joined = string.Join(" + ", parts);
            return string.Create(CultureInfo.InvariantCulture,
                $"{joined} -> p={probability:0.00} -> {verdict}.");
        }
    }
}
