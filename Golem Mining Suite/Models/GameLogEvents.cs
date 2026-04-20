using System;

namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// Base type for all events emitted by <see cref="Golem_Mining_Suite.Services.GameLogService"/>.
    /// <para>
    /// Every event carries the ISO-8601 UTC timestamp parsed from the <c>Game.log</c> line that
    /// produced it. When the source line has no timestamp (header lines on a fresh log, or
    /// synthesized events like <see cref="LogRotationEvent"/>), the service populates
    /// <see cref="Timestamp"/> with <see cref="DateTime.UtcNow"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Scope note (from <c>tasks/research/R2-game-log-mining.md</c>): since Star Citizen 4.0.2,
    /// <c>Game.log</c> only contains events involving the client player. Rock scan composition,
    /// laser fire, fracture outcomes, mined cargo quantities, refinery orders, and cargo
    /// load/unload are NOT in <c>Game.log</c>; OCR remains the authoritative source for those.
    /// This hierarchy is intentionally limited to session-bracketing signals.
    /// </remarks>
    public abstract record GameLogEvent(DateTime Timestamp);

    /// <summary>
    /// Emitted once per attach to a fresh log file: either on initial service start after seeking
    /// to the end of an existing <c>Game.log</c>, or after a detected rotation where the log has
    /// been truncated/replaced by a new game launch.
    /// </summary>
    public sealed record SessionStartEvent(DateTime Timestamp, string LogPath) : GameLogEvent(Timestamp);

    /// <summary>Legacy login response — "User Login Success" line. <paramref name="Handle"/> is the RSI handle.</summary>
    public sealed record LoginEvent(DateTime Timestamp, string Handle) : GameLogEvent(Timestamp);

    /// <summary>Quantum travel started. "-- Entity Trying To QT: &lt;name&gt;" line.</summary>
    public sealed record QtStartEvent(DateTime Timestamp, string EntityName) : GameLogEvent(Timestamp);

    /// <summary>
    /// Quantum travel ended. Inferred from a <c>&lt;Jump Drive State Changed&gt;</c> transition to
    /// <c>Idle</c> following a prior <see cref="QtStartEvent"/>.
    /// </summary>
    public sealed record QtEndEvent(DateTime Timestamp) : GameLogEvent(Timestamp);

    /// <summary>Local player death. Reason mirrors the log's <c>damage type</c> field.</summary>
    public sealed record PlayerDeathEvent(DateTime Timestamp, string ActorName, string Reason) : GameLogEvent(Timestamp);

    /// <summary>
    /// Vehicle destruction. <paramref name="DestroyLevel"/> is the post-transition level:
    /// 1 = soft-death, 2 = full destruction.
    /// </summary>
    public sealed record ShipDestroyedEvent(DateTime Timestamp, string VehicleName, int DestroyLevel) : GameLogEvent(Timestamp);

    /// <summary>
    /// Actor stall — proxy for an impending 30k server crash. <paramref name="Context"/> holds the
    /// raw <c>Type: [up|down]stream, Length: N.NNN</c> payload so downstream heuristics can gate
    /// on stall duration.
    /// </summary>
    public sealed record ActorStallEvent(DateTime Timestamp, string Context) : GameLogEvent(Timestamp);

    /// <summary>Clean disconnect / quit — any of <c>Disconnect</c>, <c>SystemQuit</c>, or <c>EndSession</c>.</summary>
    public sealed record DisconnectEvent(DateTime Timestamp) : GameLogEvent(Timestamp);

    /// <summary>
    /// Synthesized (not parsed) event: the tailer detected the log was truncated or rotated
    /// (file length shrank, or open failed with a not-found-style error), and has reopened the
    /// file from byte 0. A <see cref="SessionStartEvent"/> will typically follow.
    /// </summary>
    public sealed record LogRotationEvent(DateTime Timestamp, string LogPath) : GameLogEvent(Timestamp);
}
