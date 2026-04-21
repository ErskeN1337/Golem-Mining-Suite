using System;
using System.Collections.Generic;

namespace Golem_Mining_Suite.Models.Regolith
{
    // Reverse-engineered from Regolith Co.'s public GraphQL schema at
    // github.com/RegolithCo/RegolithCo-Common (MIT). Fields documented here mirror the .gql
    // sources in tasks/research/_regolith_schema/ — see R3-regolith-schema.md §3 for the
    // authoritative mapping table.
    //
    // Scalar rules (per Regolith codegen.yml):
    //   Timestamp  → epoch-ms long, exposed here as DateTime? (UTC)
    //   BigInt     → aUEC amount, string-or-number in JSON → decimal (safe width)
    //   JSONObject → opaque dict, not carried through the importer
    //   RockType   → string drawn from AsteroidTypeEnum ∪ DepositTypeEnum (hybrid)
    //
    // Every record is lenient: every field outside the stable key set is nullable so a
    // partial / patched export still deserializes. The importer is responsible for surfacing
    // "missing expected field" warnings — no record throws on absence.

    /// <summary>
    /// Regolith <c>User</c> (lightweight public shape; see <c>user.gql</c>).
    /// </summary>
    public sealed record RegolithUser
    {
        public string? UserId { get; init; }
        public string? ScName { get; init; }
        public string? VerifiedUserName { get; init; }
        public string? State { get; init; } // UNVERIFIED | VERIFIED
        public string? AvatarUrl { get; init; }
    }

    /// <summary>
    /// Regolith <c>Session</c> plus the nested crew / scouting / work-order lists the
    /// importer cares about. See <c>sessions.gql</c> lines 76-107.
    /// </summary>
    public sealed record RegolithSession
    {
        public string? SessionId { get; init; }
        public string? JoinId { get; init; }
        public string? OwnerId { get; init; }
        public RegolithUser? Owner { get; init; }
        public string? Name { get; init; }
        public string? Note { get; init; }
        public string? Version { get; init; }
        public string? State { get; init; } // ACTIVE | CLOSED
        public long? CreatedAt { get; init; }
        public long? UpdatedAt { get; init; }
        public long? FinishedAt { get; init; }

        public RegolithSessionSettings? SessionSettings { get; init; }
        public IReadOnlyList<RegolithSessionUser>? ActiveMembers { get; init; }
        public IReadOnlyList<RegolithPendingUser>? MentionedUsers { get; init; }
        public IReadOnlyList<RegolithWorkOrder>? WorkOrders { get; init; }
        public IReadOnlyList<RegolithScoutingFind>? Scouting { get; init; }
        public RegolithSessionSummary? Summary { get; init; }
    }

    public sealed record RegolithSessionSettings
    {
        public string? Activity { get; init; } // VEHICLE_MINING | SHIP_MINING | SALVAGE | OTHER
        public string? Location { get; init; } // SURFACE | CAVE | SPACE | RING
        public string? SystemFilter { get; init; } // STANTON | PYRO | NYX
        public string? GravityWell { get; init; }
    }

    public sealed record RegolithSessionSummary
    {
        /// <summary>Cached aUEC total as reported by Regolith. We recompute on import.</summary>
        public decimal? AUEC { get; init; }
        public double? CollectedSCU { get; init; }
        public double? YieldSCU { get; init; }
        public bool? AllPaid { get; init; }
    }

    /// <summary>
    /// Logged-in crew member within a session (<c>sessions.gql</c> <c>SessionUser</c>).
    /// </summary>
    public sealed record RegolithSessionUser
    {
        public string? SessionId { get; init; }
        public string? OwnerId { get; init; }
        public RegolithUser? Owner { get; init; }
        public bool? IsPilot { get; init; }
        public string? SessionRole { get; init; }
        public string? ShipRole { get; init; }
        public string? CaptainId { get; init; }
        public string? ShipName { get; init; }
        public string? State { get; init; } // TRAVELLING | SCOUTING | ON_SITE | AFK | REFINERY_RUN | UNKNOWN
        public string? VehicleCode { get; init; }
        public RegolithLoadout? Loadout { get; init; }
    }

    /// <summary>
    /// Mentioned-but-not-joined user (<c>PendingUser</c> in <c>sessions.gql</c>).
    /// </summary>
    public sealed record RegolithPendingUser
    {
        public string? ScName { get; init; }
        public string? CaptainId { get; init; }
        public string? SessionRole { get; init; }
        public string? ShipRole { get; init; }
    }

    /// <summary>
    /// Flattened union of Regolith's four <c>WorkOrder</c> variants
    /// (<c>ShipMiningOrder | VehicleMiningOrder | SalvageOrder | OtherOrder</c>).
    /// Carries the discriminator + all four nullable payload arrays so callers don't need a
    /// custom polymorphic JSON converter. See <c>workorders.gql</c> for field meanings.
    /// </summary>
    public sealed record RegolithWorkOrder
    {
        /// <summary>GraphQL <c>__typename</c> — one of ShipMiningOrder/VehicleMiningOrder/SalvageOrder/OtherOrder.</summary>
        public string? Typename { get; init; }

        public string? OrderId { get; init; }
        public string? SessionId { get; init; }
        public string? OwnerId { get; init; }
        public long? CreatedAt { get; init; }
        public long? UpdatedAt { get; init; }
        public string? Version { get; init; }

        public string? OrderType { get; init; } // ActivityEnum
        public string? State { get; init; }     // WorkOrderStateEnum
        public string? FailReason { get; init; }
        public string? Note { get; init; }

        public bool? IncludeTransferFee { get; init; }
        public bool? IsSold { get; init; }
        /// <summary>Manual override of calculated sale price (aUEC). Regolith encodes BigInt as string or number — we normalise to decimal.</summary>
        public decimal? ShareAmount { get; init; }
        public string? SellStore { get; init; }

        public string? SellerScName { get; init; }
        public string? SellerUserId { get; init; }

        public IReadOnlyList<RegolithWorkOrderExpense>? Expenses { get; init; }
        public IReadOnlyList<RegolithCrewShare>? CrewShares { get; init; }

        // Ship-specific
        public bool? ShareRefinedValue { get; init; }
        public bool? IsRefined { get; init; }
        public long? ProcessStartTime { get; init; }
        public int? ProcessDurationS { get; init; }
        public long? ProcessEndTime { get; init; }
        public string? Refinery { get; init; }
        public string? Method { get; init; }
        public IReadOnlyList<RegolithRefineryRow>? ShipOres { get; init; }

        // Vehicle-specific
        public IReadOnlyList<RegolithVehicleRow>? VehicleOres { get; init; }

        // Salvage-specific
        public IReadOnlyList<RegolithSalvageRow>? SalvageOres { get; init; }
    }

    public sealed record RegolithWorkOrderExpense
    {
        public string? Name { get; init; }
        public decimal? Amount { get; init; }
        public string? OwnerScName { get; init; }
    }

    public sealed record RegolithRefineryRow
    {
        public string? Ore { get; init; } // ShipOreEnum
        public int? Amt { get; init; }
        public int? Yield { get; init; }
    }

    public sealed record RegolithVehicleRow
    {
        public string? Ore { get; init; } // VehicleOreEnum
        public int? Amt { get; init; }
    }

    public sealed record RegolithSalvageRow
    {
        public string? Ore { get; init; } // SalvageOreEnum
        public int? Amt { get; init; }
    }

    /// <summary>
    /// Per-order payout split (<c>crewshares.gql</c> <c>CrewShare</c>). <c>PayeeScName</c>
    /// is always populated even for deleted users; <c>PayeeUserId</c> may dangle.
    /// </summary>
    public sealed record RegolithCrewShare
    {
        public string? SessionId { get; init; }
        public string? OrderId { get; init; }
        public long? CreatedAt { get; init; }
        public long? UpdatedAt { get; init; }
        public string? PayeeScName { get; init; }
        public string? PayeeUserId { get; init; }
        /// <summary>PERCENT | AMOUNT | SHARE.</summary>
        public string? ShareType { get; init; }
        /// <summary>0-1 for PERCENT, aUEC for AMOUNT, integer for SHARE.</summary>
        public double? Share { get; init; }
        public string? Note { get; init; }
        /// <summary>true=paid, false/null=unpaid.</summary>
        public bool? State { get; init; }
    }

    /// <summary>
    /// Flattened union of Regolith's three <c>ScoutingFind</c> variants
    /// (<c>ShipClusterFind | VehicleClusterFind | SalvageFind</c>).
    /// </summary>
    public sealed record RegolithScoutingFind
    {
        public string? Typename { get; init; }

        public string? ScoutingFindId { get; init; }
        public string? SessionId { get; init; }
        public string? OwnerId { get; init; }
        public long? CreatedAt { get; init; }
        public long? UpdatedAt { get; init; }
        public string? ClusterType { get; init; } // VEHICLE | SHIP | SALVAGE
        public string? Version { get; init; }
        public int? ClusterCount { get; init; }
        public string? Note { get; init; }
        public string? State { get; init; } // DISCOVERED | READY_FOR_WORKERS | WORKING | DEPLETED | ABANDONNED [sic]
        public string? GravityWell { get; init; }
        public bool? IncludeInSurvey { get; init; }
        public double? SurveyBonus { get; init; }
        public int? Score { get; init; }
        public int? RawScore { get; init; }
        public IReadOnlyList<string>? AttendanceIds { get; init; }

        public IReadOnlyList<RegolithShipRock>? ShipRocks { get; init; }
        public IReadOnlyList<RegolithVehicleRock>? VehicleRocks { get; init; }
        public IReadOnlyList<RegolithSalvageWreck>? Wrecks { get; init; }
    }

    public sealed record RegolithShipRock
    {
        public string? State { get; init; } // READY | DEPLETED | IGNORE
        public double? Mass { get; init; }
        public double? Inst { get; init; }
        public double? Res { get; init; }
        /// <summary>Hybrid scalar — value from AsteroidTypeEnum ∪ DepositTypeEnum.</summary>
        public string? RockType { get; init; }
        public IReadOnlyList<RegolithRockOre>? Ores { get; init; }
    }

    public sealed record RegolithVehicleRock
    {
        public double? Mass { get; init; }
        public double? Inst { get; init; }
        public double? Res { get; init; }
        public IReadOnlyList<RegolithRockOre>? Ores { get; init; }
    }

    /// <summary>Shared shape for <c>ShipRockOre</c> and <c>VehicleRockOre</c> — both are { ore, percent }.</summary>
    public sealed record RegolithRockOre
    {
        public string? Ore { get; init; }
        public double? Percent { get; init; }
    }

    public sealed record RegolithSalvageWreck
    {
        public string? State { get; init; }
        public bool? IsShip { get; init; }
        public string? ShipCode { get; init; }
        public decimal? SellableAUEC { get; init; }
        public IReadOnlyList<RegolithSalvageWreckOre>? SalvageOres { get; init; }
    }

    public sealed record RegolithSalvageWreckOre
    {
        public string? Ore { get; init; }
        public int? Scu { get; init; }
    }

    /// <summary>
    /// Ship + laser + module + gadget snapshot (<c>loadouts.gql</c> <c>MiningLoadout</c>).
    /// </summary>
    public sealed record RegolithLoadout
    {
        public string? LoadoutId { get; init; }
        public string? Name { get; init; }
        public string? Ship { get; init; } // PROSPECTOR | MOLE | GOLEM | ROC
        public RegolithUser? Owner { get; init; }
        public long? CreatedAt { get; init; }
        public long? UpdatedAt { get; init; }
        public IReadOnlyList<RegolithActiveLaserLoadout>? ActiveLasers { get; init; }
        public IReadOnlyList<string>? InventoryLasers { get; init; }
        public IReadOnlyList<string>? InventoryModules { get; init; }
        public IReadOnlyList<string>? InventoryGadgets { get; init; }
        public int? ActiveGadgetIndex { get; init; }
    }

    public sealed record RegolithActiveLaserLoadout
    {
        public string? Laser { get; init; }
        public bool? LaserActive { get; init; }
        public IReadOnlyList<string?>? Modules { get; init; }
        public IReadOnlyList<bool>? ModulesActive { get; init; }
    }

    /// <summary>
    /// Top-level GraphQL response envelope — <c>{ "data": { ... } }</c>.
    /// </summary>
    public sealed record RegolithGraphQlEnvelope<T>
    {
        public T? Data { get; init; }
        public IReadOnlyList<RegolithGraphQlError>? Errors { get; init; }
    }

    public sealed record RegolithGraphQlError
    {
        public string? Message { get; init; }
    }

    /// <summary>
    /// Shape returned by the session-by-id query.
    /// </summary>
    public sealed record RegolithSessionResponse
    {
        public RegolithSession? Session { get; init; }
    }

    /// <summary>
    /// Shape returned by the bootstrap <c>profile</c> query (used by "import everything").
    /// </summary>
    public sealed record RegolithProfileResponse
    {
        public RegolithProfile? Profile { get; init; }
    }

    public sealed record RegolithProfile
    {
        public string? UserId { get; init; }
        public string? ScName { get; init; }
        public RegolithPaginatedSessions? MySessions { get; init; }
        public RegolithPaginatedSessions? JoinedSessions { get; init; }
    }

    public sealed record RegolithPaginatedSessions
    {
        public IReadOnlyList<RegolithSession>? Items { get; init; }
        public string? NextToken { get; init; }
    }
}
