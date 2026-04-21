using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models.Regolith;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Wave 5B local-only crew session store. Consumes <see cref="ImportedSession"/> values
    /// produced by <see cref="IRegolithImporter"/> and persists them to
    /// <c>%APPDATA%\Golem Mining Suite\crew-sessions.json</c>. No cloud sync in this pass.
    /// </summary>
    /// <remarks>
    /// Intentionally thin: this is a persistence + share-math helper, not a domain model.
    /// Threadsafety is provided by a <c>SemaphoreSlim</c> around file I/O and the in-memory
    /// list so a concurrent import + UI refresh can't corrupt state.
    /// </remarks>
    public interface ICrewSessionService
    {
        /// <summary>
        /// Snapshot of the currently persisted sessions. Returned as read-only so UI
        /// bindings cannot mutate the service's internal list.
        /// </summary>
        IReadOnlyList<ImportedSession> Sessions { get; }

        /// <summary>
        /// Fired whenever the session list changes (add, remove, load). UI code observes
        /// this to refresh bindings without polling.
        /// </summary>
        event EventHandler? SessionsChanged;

        /// <summary>Reload the session list from disk, replacing the in-memory state.</summary>
        Task LoadAsync(CancellationToken ct = default);

        /// <summary>Flush the current in-memory list to disk.</summary>
        Task SaveAsync(CancellationToken ct = default);

        /// <summary>
        /// Add a session, idempotent on <see cref="ImportedSession.Id"/> — a second add with
        /// the same id is a silent no-op. Persists to disk after the mutation.
        /// </summary>
        Task AddAsync(ImportedSession session, CancellationToken ct = default);

        /// <summary>
        /// Add many sessions in one pass; the persistence write happens once at the end.
        /// Duplicates (by id) are skipped exactly like <see cref="AddAsync"/>.
        /// </summary>
        Task AddRangeAsync(IEnumerable<ImportedSession> sessions, CancellationToken ct = default);

        /// <summary>
        /// Remove a session by id. No-op if the id isn't present.
        /// </summary>
        Task RemoveAsync(string sessionId, CancellationToken ct = default);

        /// <summary>
        /// Compute the caller's share of a session's total payout based on the crew
        /// member whose <see cref="ImportedCrewMember.Handle"/> matches <paramref name="myHandle"/>
        /// (case-insensitive). Falls back to an equal split across all crew members when
        /// <paramref name="myHandle"/> is empty or unmatched — which is the right answer when
        /// we don't have enough info to do better than "everyone gets the same cut".
        /// </summary>
        decimal MyShare(ImportedSession session, string myHandle);
    }
}
