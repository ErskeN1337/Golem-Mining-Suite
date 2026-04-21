using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models.Piracy;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Counter-piracy route analyzer: given a sequence of quantum-travel legs,
    /// enumerate every known pull-point whose snare sphere the route passes
    /// through and assemble a normalised risk score.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Geometry follows the Snareplan model (R4 §1): classic point-to-line-segment
    /// perpendicular distance, Mantis LIVE radius = 20 km, ~90 s spool. A leg is
    /// "snareable" by a pull-point iff the perpendicular distance to that
    /// pull-point is less than its radius *and* the closest-approach projection
    /// lies on the segment (not an extrapolation past either endpoint).
    /// </para>
    /// <para>
    /// Pull-point data is a merge of the shipped seed file
    /// (<c>Assets/Data/piracy-seed.json</c>) and any crowdsourced reports read
    /// from Supabase. Seed coordinates are deliberately approximate — see R4's
    /// "Data-fidelity caveat" — and are expected to be refined by user reports.
    /// </para>
    /// </remarks>
    public interface IPiracyRouteAnalyzer
    {
        /// <summary>
        /// Return the merged list of pull-points (seed + crowdsourced). Result is
        /// cached internally for 10 minutes to keep the optimizer hot-path fast.
        /// </summary>
        Task<IReadOnlyList<PullPoint>> GetPullPointsAsync(CancellationToken ct = default);

        /// <summary>
        /// Synchronously score a route. Expects the caller to have already resolved
        /// pull-points via <see cref="GetPullPointsAsync"/> during VM init; this
        /// method does not block on I/O.
        /// </summary>
        RouteRisk Analyze(IEnumerable<QtLeg> route);

        /// <summary>
        /// Upload a crowdsourced pull-point report. Requires explicit user action
        /// (never called from a background loop). When Supabase is not configured
        /// the report is persisted to
        /// <c>%APPDATA%\Golem Mining Suite\pending-piracy-reports.json</c> for
        /// later upload.
        /// </summary>
        Task ReportPullPointAsync(PullPoint point, CancellationToken ct = default);
    }
}
