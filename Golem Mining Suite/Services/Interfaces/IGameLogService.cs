using System;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Abstraction over <see cref="Golem_Mining_Suite.Services.GameLogService"/>: tails the Star
    /// Citizen <c>Game.log</c> file and raises typed <see cref="GameLogEvent"/>s for session
    /// bracketing (login, QT, death, ship destruction, stalls, disconnect, log rotation).
    /// <para>
    /// Exposed as an interface so ViewModels, the session aggregator, and future test doubles
    /// can depend on it without reaching for the concrete implementation. Scope is deliberately
    /// narrow — mining-mechanic events (rock composition, fracture outcome, yields) are not in
    /// <c>Game.log</c> and remain OCR's responsibility.
    /// </para>
    /// </summary>
    public interface IGameLogService
    {
        /// <summary>
        /// Raised on the UI/calling thread-pool context whenever a parseable event is seen.
        /// Handlers must be fast; long work should be posted off-thread by the subscriber.
        /// </summary>
        event EventHandler<GameLogEvent>? GameEventReceived;

        /// <summary>
        /// Raised when the tailer attaches to a log file or detaches from it. The boolean
        /// payload is <c>true</c> while actively tailing.
        /// </summary>
        event EventHandler<bool>? TailingStatusChanged;

        /// <summary>Absolute path of the <c>Game.log</c> currently being tailed, or <c>null</c> if disarmed.</summary>
        string? CurrentLogPath { get; }

        /// <summary>Whether the service is currently tailing a file.</summary>
        bool IsTailing { get; }

        /// <summary>
        /// Begin tailing the resolved <c>Game.log</c>. Safe to call multiple times — subsequent
        /// calls are no-ops while a prior session is still running. If the log cannot be located
        /// or opened, the service logs a warning and stays in a disarmed state (no exception).
        /// </summary>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop tailing, cancel the poll loop, and release file handles. Idempotent.
        /// </summary>
        Task StopAsync();
    }
}
