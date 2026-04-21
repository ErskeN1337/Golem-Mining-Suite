namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// Discrete quality bands the 4.7 mining/refining UI cares about. Thresholds are community
    /// rules-of-thumb captured in R1-refinery-4.7.md (2026-04-21 research):
    /// <list type="bullet">
    /// <item><description>&lt; 500 — Debuff: crafted item is worse than store-bought. Sell raw.</description></item>
    /// <item><description>500..649 — Baseline: break-even with store-bought.</description></item>
    /// <item><description>650..699 — Good: community "use it" threshold.</description></item>
    /// <item><description>700..899 — Keeper: stockpile for crafting.</description></item>
    /// <item><description>&gt;= 900 — Endgame: high-end stockpile.</description></item>
    /// </list>
    /// CIG has not published the underlying formula. Bands are an overlay on an opaque 0-1000 score.
    /// </summary>
    public enum QualityTier
    {
        Debuff = 0,
        Baseline = 1,
        Good = 2,
        Keeper = 3,
        Endgame = 4,
    }

    /// <summary>
    /// A Star Citizen 4.7 ore/refined-material quality score. Stored as an integer clamped to
    /// [0, 1000]. Propagates 1:1 through refining — never averaged, never merged across stacks.
    /// </summary>
    /// <remarks>
    /// See <c>tasks/research/R1-refinery-4.7.md</c> for source citations. This is a readonly
    /// record struct so it can be used as a nullable value type on data models without forcing
    /// allocations in hot paths (inventory lists, rock-scan results, etc.).
    /// </remarks>
    public readonly record struct QualityScore
    {
        /// <summary>Minimum allowed value (inclusive).</summary>
        public const int MinValue = 0;

        /// <summary>Maximum allowed value (inclusive).</summary>
        public const int MaxValue = 1000;

        /// <summary>Raw 0-1000 quality score, clamped on construction.</summary>
        public int Value { get; }

        /// <summary>
        /// Create a quality score. Values outside <see cref="MinValue"/>..<see cref="MaxValue"/>
        /// are silently clamped — a non-throwing constructor is easier for callers that parse
        /// user-entered text or third-party JSON where an out-of-range integer should not
        /// bring down the whole view.
        /// </summary>
        public QualityScore(int value)
        {
            if (value < MinValue) value = MinValue;
            else if (value > MaxValue) value = MaxValue;
            Value = value;
        }

        /// <summary>Tier this score falls in — thresholds documented on <see cref="QualityTier"/>.</summary>
        public QualityTier Tier => Value switch
        {
            < 500 => QualityTier.Debuff,
            < 650 => QualityTier.Baseline,
            < 700 => QualityTier.Good,
            < 900 => QualityTier.Keeper,
            _ => QualityTier.Endgame,
        };

        /// <summary>True when the score produces a worse-than-store-bought crafted item.</summary>
        public bool IsDebuff => Value < 500;

        /// <summary>True when the community "keep for crafting" bar is met.</summary>
        public bool IsKeeper => Value >= 700;

        /// <summary>True for the high-end stockpile tier.</summary>
        public bool IsEndgame => Value >= 900;

        public override string ToString() => Value.ToString();
    }
}
