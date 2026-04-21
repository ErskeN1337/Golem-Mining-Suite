using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// In-memory tracker that fires a desktop toast via <see cref="IToastNotificationService"/>
    /// when a tracked refinery order's completion timestamp has passed. The active-order list
    /// is persisted to <c>%APPDATA%\Golem Mining Suite\refinery-orders.json</c> so in-flight
    /// orders survive an app restart — the motivating UX per research R3 is "miners love
    /// knowing when their pickup is ready", which fails if closing the app silently drops the
    /// tracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The internal timer ticks every 30 seconds. Tests do not wait on the timer — they call
    /// <see cref="CheckOverdueOrdersAsync"/> directly, which is the single code path the timer
    /// drives anyway.
    /// </para>
    /// <para>
    /// Disposal stops the timer and flushes pending persistence. Both <see cref="IDisposable"/>
    /// and <see cref="IAsyncDisposable"/> are implemented so DI containers that prefer the async
    /// variant (newer <c>Microsoft.Extensions.DependencyInjection</c> versions) get a clean
    /// shutdown.
    /// </para>
    /// </remarks>
    public sealed class RefineryOrderWatcher : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Timer tick interval. 30s is a sensible middle-ground — short enough that a
        /// completed order is surfaced within half a minute, long enough that idle CPU is
        /// negligible.
        /// </summary>
        internal static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(30);

        private readonly IToastNotificationService _toast;
        private readonly ILogger<RefineryOrderWatcher> _logger;
        private readonly string _persistencePath;
        private readonly Func<DateTime> _utcNow;

        // Single lock guards both the list and the persistence write. The list is small
        // (a handful of in-flight orders per session) so coarse locking is fine.
        private readonly object _gate = new();
        private readonly List<TrackedRefineryOrder> _orders = new();
        private readonly Timer? _timer;
        private bool _disposed;

        /// <summary>
        /// Production constructor — persists to <c>%APPDATA%\Golem Mining Suite\refinery-orders.json</c>
        /// and starts the 30-second tick timer.
        /// </summary>
        public RefineryOrderWatcher(
            IToastNotificationService toast,
            ILogger<RefineryOrderWatcher> logger)
            : this(toast, logger, DefaultPersistencePath(), () => DateTime.UtcNow, startTimer: true)
        {
        }

        /// <summary>
        /// Test-visible constructor. Lets tests inject a temp-file persistence path, a frozen
        /// clock, and skip the background timer (tests drive <see cref="CheckOverdueOrdersAsync"/>
        /// directly so they never rely on wall-clock ticks).
        /// </summary>
        internal RefineryOrderWatcher(
            IToastNotificationService toast,
            ILogger<RefineryOrderWatcher> logger,
            string persistencePath,
            Func<DateTime> utcNow,
            bool startTimer)
        {
            _toast = toast ?? throw new ArgumentNullException(nameof(toast));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _persistencePath = persistencePath ?? throw new ArgumentNullException(nameof(persistencePath));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));

            LoadFromDisk();

            if (startTimer)
            {
                // Fire-and-forget async callback: the timer invokes CheckOverdueOrdersAsync on a
                // thread-pool thread; wrap in try/catch so a synchronous throw cannot escape
                // the timer and tear down the process.
                _timer = new Timer(
                    callback: _ =>
                    {
                        try { _ = CheckOverdueOrdersAsync(); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Refinery-order tick threw synchronously");
                        }
                    },
                    state: null,
                    dueTime: DefaultTickInterval,
                    period: DefaultTickInterval);
            }
        }

        /// <summary>
        /// Snapshot of the currently tracked orders, most imminent first. Returned as a
        /// read-only list so callers cannot mutate internal state.
        /// </summary>
        public IReadOnlyList<TrackedRefineryOrder> Active
        {
            get
            {
                lock (_gate)
                {
                    return new ReadOnlyCollection<TrackedRefineryOrder>(
                        _orders.OrderBy(o => o.CompleteAtUtc).ToList());
                }
            }
        }

        /// <summary>
        /// Start tracking a refinery order. Idempotent on <see cref="TrackedRefineryOrder.OrderId"/>
        /// — re-tracking the same id replaces the existing entry (useful if the completion
        /// estimate changes).
        /// </summary>
        public void Track(TrackedRefineryOrder order)
        {
            ArgumentNullException.ThrowIfNull(order);

            lock (_gate)
            {
                _orders.RemoveAll(o => o.OrderId == order.OrderId);
                _orders.Add(order);
                PersistLocked();
            }
        }

        /// <summary>
        /// Stop tracking a specific order. No-op if the id isn't currently tracked — callers
        /// can treat this as a safe "dismiss" primitive.
        /// </summary>
        public void Untrack(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return;

            lock (_gate)
            {
                int removed = _orders.RemoveAll(o => o.OrderId == orderId);
                if (removed > 0) PersistLocked();
            }
        }

        /// <summary>
        /// Inspect every tracked order; fire <see cref="IToastNotificationService.ShowRefineryReady"/>
        /// for any whose completion has passed, then drop them from the active list. This is
        /// what the internal timer invokes on every tick — tests call it directly rather than
        /// waiting on the timer.
        /// </summary>
        /// <returns>A completed <see cref="Task"/>. The async signature is kept so the method
        /// composes cleanly with the timer callback and leaves room for future I/O-bound work
        /// (e.g. re-polling UEX for order status).</returns>
        public Task CheckOverdueOrdersAsync()
        {
            List<TrackedRefineryOrder> due;

            lock (_gate)
            {
                DateTime now = _utcNow();
                due = _orders.Where(o => o.CompleteAtUtc <= now).ToList();
                if (due.Count == 0) return Task.CompletedTask;

                foreach (var order in due)
                {
                    _orders.RemoveAll(o => o.OrderId == order.OrderId);
                }
                PersistLocked();
            }

            // Fire toasts *outside* the lock — the toast call may touch COM / Windows UI and we
            // don't want to hold the list lock across that.
            foreach (var order in due)
            {
                try
                {
                    _toast.ShowRefineryReady(order.RefineryName, order.OreName, order.QuantitySCU);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Toast dispatch failed for completed order {OrderId}", order.OrderId);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Resolve the production persistence path under <c>%APPDATA%</c>. Extracted so the
        /// test constructor can bypass it cleanly without touching the user's real AppData.
        /// </summary>
        internal static string DefaultPersistencePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Golem Mining Suite");
            return Path.Combine(dir, "refinery-orders.json");
        }

        /// <summary>
        /// Persistence helper — must be called with <see cref="_gate"/> held. Writes the
        /// current order list as JSON and swallows IO errors (persistence is a convenience,
        /// losing it must not take down the watcher).
        /// </summary>
        private void PersistLocked()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(_orders, s_jsonOptions);
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist refinery orders to {Path}", _persistencePath);
            }
        }

        private void LoadFromDisk()
        {
            try
            {
                if (!File.Exists(_persistencePath)) return;

                string json = File.ReadAllText(_persistencePath);
                var loaded = JsonSerializer.Deserialize<List<TrackedRefineryOrder>>(json, s_jsonOptions);
                if (loaded is null) return;

                lock (_gate)
                {
                    _orders.Clear();
                    _orders.AddRange(loaded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to load refinery orders from {Path}; starting with empty list",
                    _persistencePath);
            }
        }

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;

            if (_timer is not null)
            {
                // Timer has no async dispose on net8 — wrap the sync dispose so callers can
                // still `await using` us without surprises.
                return new ValueTask(_timer.DisposeAsync().AsTask());
            }
            return ValueTask.CompletedTask;
        }
    }
}
