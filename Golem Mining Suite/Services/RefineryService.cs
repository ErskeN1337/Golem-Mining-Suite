using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services
{
    public class RefineryService : IRefineryService
    {
        private readonly Dictionary<string, RefineryMethod> _refineryMethods = new();
        private readonly Dictionary<string, Dictionary<string, double>> _refineryYields = new();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RefineryService> _logger;

        public RefineryService(IHttpClientFactory httpClientFactory, ILogger<RefineryService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<RefineryMethod>> GetRefineryMethodsAsync()
        {
            if (_refineryMethods.Count > 0) return new List<RefineryMethod>(_refineryMethods.Values);

            try
            {
                var client = _httpClientFactory.CreateClient("uex");
                var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_methods");
                var jsonDoc = JsonDocument.Parse(response);
                var methods = jsonDoc.RootElement.GetProperty("data");

                foreach (var method in methods.EnumerateArray())
                {
                    string? name = method.GetProperty("name").GetString();
                    string? code = method.GetProperty("code").GetString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code)) continue;

                    int yieldRating = method.GetProperty("rating_yield").GetInt32();
                    int costRating = method.GetProperty("rating_cost").GetInt32();
                    int speedRating = method.GetProperty("rating_speed").GetInt32();

                    // Calculate percentages based on ratings
                    double yieldPercent = yieldRating == 3 ? 70 : (yieldRating == 2 ? 50 : 30);
                    double costPercent = costRating == 3 ? 15 : (costRating == 2 ? 10 : 7);

                    _refineryMethods[name] = new RefineryMethod
                    {
                        Name = name,
                        Code = code,
                        YieldBonus = yieldPercent,
                        CostPercent = costPercent,
                        YieldRating = yieldRating,
                        CostRating = costRating,
                        SpeedRating = speedRating
                    };
                }
            }
            catch (Exception)
            {
                LoadFallbackRefineryData();
            }

            return new List<RefineryMethod>(_refineryMethods.Values);
        }

        public async Task<Dictionary<string, Dictionary<string, double>>> GetRefineryYieldsAsync()
        {
            if (_refineryYields.Count > 0) return _refineryYields;

            try
            {
                var client = _httpClientFactory.CreateClient("uex");
                var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_yields");
                var jsonDoc = JsonDocument.Parse(response);
                var yields = jsonDoc.RootElement.GetProperty("data");

                foreach (var yieldData in yields.EnumerateArray())
                {
                    string? terminal = yieldData.GetProperty("terminal_name").GetString();
                    string? commodity = yieldData.GetProperty("commodity_name").GetString();
                    int value = yieldData.GetProperty("value").GetInt32();

                    if (string.IsNullOrEmpty(terminal) || string.IsNullOrEmpty(commodity)) continue;

                    if (!_refineryYields.ContainsKey(terminal))
                    {
                        _refineryYields[terminal] = new Dictionary<string, double>();
                    }

                    _refineryYields[terminal][commodity] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch UEX refinery yields; returning whatever was cached");
            }

            // If UEX returned no refineries (offline first boot, API change, etc.), seed the
            // Star Citizen 4.7 station list so the UI dropdown still has sensible options.
            // Empty yield dictionaries per station = "no bonus known" which the calculator
            // treats as a plain 0% bonus — safer than hiding the station entirely.
            if (_refineryYields.Count == 0)
            {
                LoadFallbackRefineryYields();
            }

            return _refineryYields;
        }

        /// <summary>
        /// Apply a heuristic quality multiplier to a base per-unit price. See
        /// <see cref="QualityScore"/> for the tier bands and R1-refinery-4.7.md for sourcing.
        /// </summary>
        /// <remarks>
        /// Multipliers are a first-pass heuristic — CIG has not published a price curve and
        /// the live market for quality-tagged materials is still thin. Will be tuned as
        /// community data stabilises. A <c>null</c> quality (unknown) is treated as 1.0x so
        /// pre-4.7 code paths are unaffected.
        /// </remarks>
        public decimal EffectiveValue(decimal basePricePerUnit, QualityScore? quality)
        {
            decimal multiplier = QualityMultiplier(quality);
            return basePricePerUnit * multiplier;
        }

        /// <summary>
        /// Heuristic multiplier lookup for <see cref="EffectiveValue"/>. Exposed as internal so
        /// the UI can show "1.40x" on the quality badge without re-deriving the bands.
        /// </summary>
        internal static decimal QualityMultiplier(QualityScore? quality)
        {
            if (quality is null) return 1.0m;

            return quality.Value.Tier switch
            {
                QualityTier.Debuff => 0.8m,     // < 500 — crafted output is inferior
                QualityTier.Baseline => 1.0m,   // 500..649 — break-even with store-bought
                QualityTier.Good => 1.15m,      // 650..699 — community "use it"
                QualityTier.Keeper => 1.4m,     // 700..899 — stockpile for crafting
                QualityTier.Endgame => 2.0m,    // >= 900 — high-end stockpile
                _ => 1.0m,
            };
        }

        private void LoadFallbackRefineryData()
        {
            _refineryMethods["Dinyx Solvents"] = new RefineryMethod { Name = "Dinyx Solvents", Code = "DINYX", YieldBonus = 70, CostPercent = 15 };
            _refineryMethods["Cormack Method"] = new RefineryMethod { Name = "Cormack Method", Code = "CORMACK", YieldBonus = 50, CostPercent = 10 };
            _refineryMethods["XCR Reaction"] = new RefineryMethod { Name = "XCR Reaction", Code = "XCR", YieldBonus = 30, CostPercent = 7 };
        }

        /// <summary>
        /// Seed the <see cref="_refineryYields"/> dictionary with the Star Citizen 4.7 station
        /// roster when the UEX API is unreachable. Values are empty per-commodity maps — this
        /// is a *name-only* fallback so the refinery dropdown has options. 4.7 additions
        /// <c>Pyro Gateway</c>, <c>Ruin Station</c>, and <c>Terra Gateway</c> are included per
        /// R1-refinery-4.7.md §4.
        /// </summary>
        private void LoadFallbackRefineryYields()
        {
            // Stanton
            _refineryYields["ARC-L1"] = new Dictionary<string, double>();
            _refineryYields["ARC-L2"] = new Dictionary<string, double>();
            _refineryYields["ARC-L4"] = new Dictionary<string, double>();
            _refineryYields["CRU-L1"] = new Dictionary<string, double>();
            _refineryYields["HUR-L1 (Green Glade)"] = new Dictionary<string, double>();
            _refineryYields["HUR-L2 (Faithful Dream)"] = new Dictionary<string, double>();
            _refineryYields["MIC-L1 (Shallow Frontier)"] = new Dictionary<string, double>();
            _refineryYields["MIC-L2 (Long Forest)"] = new Dictionary<string, double>();
            _refineryYields["MIC-L5"] = new Dictionary<string, double>();
            _refineryYields["Terra Gateway"] = new Dictionary<string, double>();

            // Pyro (4.7 LIVE per R1)
            _refineryYields["Checkmate"] = new Dictionary<string, double>();
            _refineryYields["Orbituary"] = new Dictionary<string, double>();
            _refineryYields["Pyro Gateway"] = new Dictionary<string, double>();
            _refineryYields["Ruin Station"] = new Dictionary<string, double>();
            _refineryYields["Stanton Gateway (Pyro)"] = new Dictionary<string, double>();

            // Nyx
            _refineryYields["Levski"] = new Dictionary<string, double>();
        }
    }
}
