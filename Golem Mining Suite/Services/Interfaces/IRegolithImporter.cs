using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models.Regolith;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Ingest path for Regolith Co. data. Regolith shuts down 2026-06-01 — being first to ship
    /// a working importer is the acquisition wedge for its ~1,200 active users. Supports two
    /// input modes (see <c>tasks/research/R3-regolith-schema.md</c> §4):
    /// <list type="number">
    /// <item><description><b>File drop</b> — user downloads per-session JSON from Regolith's UI before shutdown.</description></item>
    /// <item><description><b>Live API pull</b> — user pastes a personal API key; we POST GraphQL to <c>api.regolith.rocks</c>.</description></item>
    /// </list>
    /// All methods degrade gracefully on partial data: missing-field events are recorded in
    /// <see cref="RegolithImportResult.Warnings"/>, never thrown. The importer returns the
    /// imported sessions in memory; downstream persistence is Wave 5B's job.
    /// </summary>
    public interface IRegolithImporter
    {
        /// <summary>
        /// Parse a single per-session JSON export (the blob Regolith's "Download JSON" button
        /// emits). Empty / whitespace-only input yields a zero-session result, not an exception.
        /// </summary>
        Task<RegolithImportResult> ImportFromFileAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// Pull one session by id from <c>api.regolith.rocks</c> using the caller's personal
        /// <c>x-api-key</c> token. Uses the named HttpClient "regolith" (to be wired into DI
        /// by a later task — this service assumes the factory is already registered).
        /// </summary>
        Task<RegolithImportResult> ImportFromApiAsync(string apiKey, string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Bootstrap path: enumerate <c>profile.mySessions</c> + <c>profile.joinedSessions</c>
        /// and pull each one. <paramref name="progress"/> receives the running count of
        /// sessions successfully imported (useful for a wizard progress bar). Rate-limit cap
        /// is 3,600 req/day per API key — this method does not enforce it; the caller should.
        /// </summary>
        Task<RegolithImportResult> ImportAllFromApiAsync(string apiKey, IProgress<int>? progress = null, CancellationToken ct = default);
    }

    /// <summary>
    /// Aggregate result returned by every import path. All counts are cumulative across the
    /// invocation; <see cref="Sessions"/> carries the full session payload for Wave 5B.
    /// </summary>
    public sealed record RegolithImportResult(
        int SessionsImported,
        int WorkOrdersImported,
        int ScoutingFindsImported,
        decimal TotalAuec,
        IReadOnlyList<string> Warnings)
    {
        /// <summary>Normalised, tool-agnostic session records ready for persistence.</summary>
        public IReadOnlyList<ImportedSession> Sessions { get; init; } = Array.Empty<ImportedSession>();

        /// <summary>Empty / no-op result — handy for early-return paths.</summary>
        public static RegolithImportResult Empty { get; } =
            new RegolithImportResult(0, 0, 0, 0m, Array.Empty<string>());
    }
}
