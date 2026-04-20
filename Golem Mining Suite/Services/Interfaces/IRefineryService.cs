using Golem_Mining_Suite.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IRefineryService
    {
        Task<List<RefineryMethod>> GetRefineryMethodsAsync();
        Task<Dictionary<string, Dictionary<string, double>>> GetRefineryYieldsAsync();

        /// <summary>
        /// Apply the Star Citizen 4.7 quality multiplier to a base per-unit price.
        /// </summary>
        /// <param name="basePricePerUnit">Raw refined-material price (aUEC per unit).</param>
        /// <param name="quality">Quality score (0-1000) or <c>null</c> when unknown.</param>
        /// <returns>The quality-adjusted effective value.</returns>
        /// <remarks>
        /// The multipliers are heuristic — CIG has not published a price curve. See
        /// <see cref="RefineryService.EffectiveValue(decimal, Models.QualityScore?)"/> for the
        /// mapping. Will be tuned once live market data stabilises.
        /// </remarks>
        decimal EffectiveValue(decimal basePricePerUnit, QualityScore? quality);
    }
}
