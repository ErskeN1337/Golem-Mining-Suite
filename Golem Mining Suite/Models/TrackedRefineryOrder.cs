using System;

namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// An in-progress refinery order being tracked so the app can fire a desktop toast
    /// when <see cref="CompleteAtUtc"/> passes. Persisted to
    /// <c>%APPDATA%\Golem Mining Suite\refinery-orders.json</c> so trackers survive an
    /// app restart.
    /// </summary>
    /// <param name="OrderId">Stable identifier (typically UEX order id or a GUID).</param>
    /// <param name="RefineryName">Station the order was placed at (e.g. "ARC-L1").</param>
    /// <param name="OreName">Refined commodity name (e.g. "Quantanium").</param>
    /// <param name="QuantitySCU">Expected yield in SCU once the order completes.</param>
    /// <param name="CompleteAtUtc">UTC timestamp at which the order becomes ready for pickup.</param>
    public sealed record TrackedRefineryOrder(
        string OrderId,
        string RefineryName,
        string OreName,
        decimal QuantitySCU,
        DateTime CompleteAtUtc);
}
