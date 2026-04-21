using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models.Regolith;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// JSON-on-disk implementation of <see cref="ICrewSessionService"/>. Stores at
    /// <c>%APPDATA%\Golem Mining Suite\crew-sessions.json</c> via <c>System.Text.Json</c>.
    /// </summary>
    /// <remarks>
    /// Concurrency model: a single <see cref="SemaphoreSlim"/> guards both the in-memory
    /// list and the file I/O. Sessions list is small (dozens, not thousands) so coarse
    /// locking is fine and keeps the reasoning simple — every mutating path is
    /// read-modify-write-persist under the same lock.
    /// </remarks>
    public sealed class CrewSessionService : ICrewSessionService, IDisposable
    {
        private readonly ILogger<CrewSessionService> _logger;
        private readonly string _persistencePath;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly List<ImportedSession> _sessions = new();
        private bool _disposed;

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Production constructor — persists under <c>%APPDATA%</c>.</summary>
        public CrewSessionService(ILogger<CrewSessionService> logger)
            : this(logger, DefaultPersistencePath())
        {
        }

        /// <summary>
        /// Test-visible constructor. Lets tests inject a temp-file persistence path without
        /// touching the user's real AppData.
        /// </summary>
        internal CrewSessionService(ILogger<CrewSessionService> logger, string persistencePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistencePath = persistencePath ?? throw new ArgumentNullException(nameof(persistencePath));
        }

        public event EventHandler? SessionsChanged;

        public IReadOnlyList<ImportedSession> Sessions
        {
            get
            {
                // Snapshot under lock: callers iterate on their own thread and must not see
                // a mutating list. Returning a fresh list is cheaper than any concurrency
                // primitive on a list this size.
                _gate.Wait();
                try
                {
                    return _sessions.ToArray();
                }
                finally
                {
                    _gate.Release();
                }
            }
        }

        public async Task LoadAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _sessions.Clear();
                if (!File.Exists(_persistencePath))
                {
                    // First-run / never-imported — legitimate empty state, not an error.
                    return;
                }

                string json;
                try
                {
                    json = await File.ReadAllTextAsync(_persistencePath, ct).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to read crew sessions from {Path}; starting with empty list",
                        _persistencePath);
                    return;
                }

                if (string.IsNullOrWhiteSpace(json)) return;

                try
                {
                    var loaded = JsonSerializer.Deserialize<List<ImportedSession>>(json, s_jsonOptions);
                    if (loaded is not null)
                    {
                        _sessions.AddRange(loaded);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "Crew sessions file at {Path} was not valid JSON; starting with empty list",
                        _persistencePath);
                }
            }
            finally
            {
                _gate.Release();
            }

            RaiseSessionsChanged();
        }

        public async Task SaveAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await PersistLockedAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task AddAsync(ImportedSession session, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(session);

            bool mutated;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                mutated = AddUnderLock(session);
                if (mutated)
                {
                    await PersistLockedAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }

            if (mutated) RaiseSessionsChanged();
        }

        public async Task AddRangeAsync(IEnumerable<ImportedSession> sessions, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(sessions);

            bool mutated = false;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                foreach (var session in sessions)
                {
                    if (session is null) continue;
                    mutated |= AddUnderLock(session);
                }

                if (mutated)
                {
                    await PersistLockedAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }

            if (mutated) RaiseSessionsChanged();
        }

        public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            bool mutated;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                int removed = _sessions.RemoveAll(s => string.Equals(s.Id, sessionId, StringComparison.Ordinal));
                mutated = removed > 0;
                if (mutated)
                {
                    await PersistLockedAsync(ct).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }

            if (mutated) RaiseSessionsChanged();
        }

        public decimal MyShare(ImportedSession session, string myHandle)
        {
            ArgumentNullException.ThrowIfNull(session);

            int crewCount = session.Crew?.Count ?? 0;
            decimal total = session.TotalPayoutAuec;

            // No crew at all → nothing meaningful to split.
            if (crewCount == 0) return 0m;

            // Handle not configured → naive equal-split fallback (documented on the interface).
            if (string.IsNullOrWhiteSpace(myHandle))
            {
                return total / crewCount;
            }

            // Match by handle, case-insensitive. If the crew member's contribution percent
            // is > 0 we use it; otherwise we fall back to equal split. The fallback catches
            // imports where Regolith hadn't assigned PERCENT shares yet (e.g. live sessions).
            var me = session.Crew!.FirstOrDefault(c =>
                string.Equals(c.Handle, myHandle, StringComparison.OrdinalIgnoreCase));

            if (me is null)
            {
                // Unknown handle → fall back to equal-split rather than zero, so a
                // mis-configured handle doesn't silently read as "I earned nothing".
                return total / crewCount;
            }

            if (me.ContributionPct > 0m)
            {
                return Math.Round(total * (me.ContributionPct / 100m), 2, MidpointRounding.AwayFromZero);
            }

            return total / crewCount;
        }

        // ---------------------------------------------------------------------------------
        // Internals
        // ---------------------------------------------------------------------------------

        /// <summary>Adds under lock; returns true if the session was actually inserted.</summary>
        private bool AddUnderLock(ImportedSession session)
        {
            if (_sessions.Any(s => string.Equals(s.Id, session.Id, StringComparison.Ordinal)))
            {
                return false;
            }
            _sessions.Add(session);
            return true;
        }

        private async Task PersistLockedAsync(CancellationToken ct)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_sessions, s_jsonOptions);
                await File.WriteAllTextAsync(_persistencePath, json, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Persistence failures are logged but never thrown — losing a save is
                // better than crashing the UI on a transient file lock.
                _logger.LogWarning(ex,
                    "Failed to persist crew sessions to {Path}", _persistencePath);
            }
        }

        private void RaiseSessionsChanged()
        {
            try
            {
                SessionsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // Handler misbehaviour must not propagate back into the service.
                _logger.LogWarning(ex, "SessionsChanged handler threw");
            }
        }

        /// <summary>
        /// Resolve the production persistence path under <c>%APPDATA%</c>. Extracted so
        /// the test constructor can bypass it cleanly.
        /// </summary>
        internal static string DefaultPersistencePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Golem Mining Suite");
            return Path.Combine(dir, "crew-sessions.json");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gate.Dispose();
        }
    }
}
