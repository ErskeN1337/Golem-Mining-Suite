using System;
using System.Collections.Generic;

namespace Golem_Mining_Suite.Models.Piracy
{
    /// <summary>
    /// Minimal 3-D vector used by the QT route geometry. Units are kilometres; we
    /// never hit ranges where <see cref="double"/> precision becomes a problem even
    /// across cross-system Pyro hauls (~70 Gm ≈ 7e7 km, well under double's safe
    /// integer range).
    /// </summary>
    /// <remarks>
    /// R4 (tasks/research/R4-pyro-qt-geometry.md) documents why we cannot ship
    /// precise (x,y,z) Pyro coordinates: CIG does not publish them. Seed data in
    /// <c>piracy-seed.json</c> is therefore approximate / anchor-relative; real
    /// positions arrive via crowdsourced reports to the Supabase
    /// <c>pull_point_reports</c> table.
    /// </remarks>
    public sealed record Vec3(double X, double Y, double Z)
    {
        public Vec3 Plus(Vec3 b) => new(X + b.X, Y + b.Y, Z + b.Z);
        public Vec3 Minus(Vec3 b) => new(X - b.X, Y - b.Y, Z - b.Z);
        public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public double Magnitude() => Math.Sqrt(Dot(this));
    }

    /// <summary>
    /// A single quantum-travel leg — one straight-line segment between two named
    /// markers (body, station, Lagrange anchor, or custom waypoint). A miner's
    /// route is a sequence of these.
    /// </summary>
    public sealed record QtLeg(string FromName, Vec3 From, string ToName, Vec3 To);

    /// <summary>
    /// A candidate snare location: station approach, Lagrange corner, jump-point
    /// corridor, or crowdsourced hotspot. See R4 §5 for the shipped seed schema.
    /// </summary>
    public sealed record PullPoint
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required Vec3 Position { get; init; }

        /// <summary>Snare radius in kilometres. Mantis LIVE = 20 km.</summary>
        public double RadiusKm { get; init; } = 20.0;

        /// <summary>
        /// 0..100; higher = more community reports / stronger signal that the spot
        /// is actively camped. Folded into risk score as <c>notoriety / 100</c>.
        /// </summary>
        public int Notoriety { get; init; } = 50;

        public string? LastReportedBy { get; init; }
        public DateTime? LastReportedAt { get; init; }

        /// <summary>Provenance: "seed" | "crowdsourced" | "self".</summary>
        public string Source { get; init; } = "seed";
    }

    /// <summary>
    /// Aggregated risk assessment for a user's planned route. Contains the raw
    /// per-leg/per-pull-point hits plus a normalised 0..100 score and a short
    /// human-readable summary suitable for a status bar or tooltip.
    /// </summary>
    public sealed record RouteRisk
    {
        public required IReadOnlyList<QtLeg> Legs { get; init; }
        public required IReadOnlyList<PullPointHit> Hits { get; init; }

        /// <summary>Clamped to [0, 100].</summary>
        public double TotalRiskScore { get; init; }

        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// One leg/pull-point intersection record. <see cref="PerpendicularDistanceKm"/>
    /// is the classic point-to-line-segment distance; <see cref="ChordLengthKm"/>
    /// is the length of the vulnerable interval on the QT line where the ship
    /// passes inside the snare sphere.
    /// </summary>
    public sealed record PullPointHit(
        QtLeg Leg,
        PullPoint PullPoint,
        double PerpendicularDistanceKm,
        double ChordLengthKm,
        bool IsOnSegment);
}
