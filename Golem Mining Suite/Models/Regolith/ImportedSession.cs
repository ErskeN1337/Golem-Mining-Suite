using System;
using System.Collections.Generic;

namespace Golem_Mining_Suite.Models.Regolith
{
    /// <summary>
    /// Internal, tool-agnostic representation of a crew mining session produced by
    /// <see cref="Golem_Mining_Suite.Services.IRegolithImporter"/> and consumed in Wave 5B by
    /// <c>CrewSessionService</c>. Records here are intentionally flatter than Regolith's
    /// GraphQL types — once a session is "imported" the source shape no longer matters.
    /// </summary>
    public sealed record ImportedSession
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required DateTime StartedAt { get; init; }
        public DateTime? FinishedAt { get; init; }
        public required IReadOnlyList<ImportedCrewMember> Crew { get; init; }
        public required IReadOnlyList<ImportedWorkOrder> WorkOrders { get; init; }
        public required IReadOnlyList<RockScan> ScoutingFinds { get; init; }
        public required decimal TotalPayoutAuec { get; init; }

        /// <summary>Origin label: "Regolith", "Golem", etc. Preserved so re-imports stay idempotent.</summary>
        public required string SourceTool { get; init; }
    }

    /// <summary>Contributing crew member after merge of registered + pending users.</summary>
    /// <param name="Id">Stable identifier — Regolith userId for registered users, scName fallback for pending ones.</param>
    /// <param name="Handle">Star Citizen in-game name (scName). Always populated.</param>
    /// <param name="ContributionPct">
    /// Percentage share of total payout attributed to this crew member, in [0, 100].
    /// For Regolith this is the sum of the member's PERCENT crew-shares across all work orders
    /// in the session; AMOUNT/SHARE splits are normalised against the session total first.
    /// </param>
    public sealed record ImportedCrewMember(string Id, string Handle, decimal ContributionPct);

    /// <summary>Work order flattened to the fields Wave 5B needs — one row per primary ore.</summary>
    /// <param name="Id">External id (Regolith orderId). Preserve for idempotent re-import.</param>
    /// <param name="Kind">ActivityEnum label: "SHIP_MINING" | "VEHICLE_MINING" | "SALVAGE" | "OTHER".</param>
    /// <param name="OreCode">Primary ore of the order (e.g. "QUANTANIUM", "HADANITE", "RMC"). Empty for OTHER.</param>
    /// <param name="Amount">Total units of the primary ore (aggregate of ore rows).</param>
    /// <param name="SellPrice">Sale amount in aUEC (Regolith <c>shareAmount</c>; 0 when the order hasn't been sold).</param>
    /// <param name="Refinery">RefineryEnum label when Kind=SHIP_MINING; null otherwise.</param>
    public sealed record ImportedWorkOrder(
        string Id,
        string Kind,
        string OreCode,
        decimal Amount,
        decimal SellPrice,
        string? Refinery);
}
