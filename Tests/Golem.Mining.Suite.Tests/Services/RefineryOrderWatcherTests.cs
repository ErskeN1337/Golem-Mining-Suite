using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Tests for Wave 5C's <see cref="RefineryOrderWatcher"/>. Every test uses a fake toast
    /// service (<see cref="FakeToastNotificationService"/>) that records calls instead of
    /// popping real OS toasts, a temp-file persistence path, and a frozen-clock injection
    /// so we never rely on wall-clock ticks.
    /// </summary>
    public class RefineryOrderWatcherTests : IDisposable
    {
        private readonly string _tempPath;

        public RefineryOrderWatcherTests()
        {
            // Unique temp file per test run — prevents cross-test contamination when xUnit
            // runs test classes in parallel.
            _tempPath = Path.Combine(
                Path.GetTempPath(),
                $"golem-refinery-orders-{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
            catch { /* cleanup-best-effort */ }
        }

        [Fact]
        public async Task CheckOverdueOrdersAsync_OrderAlreadyComplete_FiresToastAndRemoves()
        {
            // Order completed one minute before "now" — the watcher must fire exactly one
            // toast and drop the entry from the active list.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder(
                OrderId: "ord-1",
                RefineryName: "ARC-L1",
                OreName: "Quantanium",
                QuantitySCU: 42m,
                CompleteAtUtc: now.AddMinutes(-1)));

            await sut.CheckOverdueOrdersAsync();

            fakeToast.RefineryReadyCalls.Should().ContainSingle();
            fakeToast.RefineryReadyCalls[0].Should().Be(("ARC-L1", "Quantanium", 42m));
            sut.Active.Should().BeEmpty("a fired order must leave the active list");
        }

        [Fact]
        public async Task CheckOverdueOrdersAsync_FutureCompletion_DoesNotFire()
        {
            // Order completes in an hour. No toast should fire on the current tick, and the
            // order must remain in the active list.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder(
                OrderId: "ord-2",
                RefineryName: "Pyro Gateway",
                OreName: "Laranite",
                QuantitySCU: 12m,
                CompleteAtUtc: now.AddHours(1)));

            await sut.CheckOverdueOrdersAsync();

            fakeToast.RefineryReadyCalls.Should().BeEmpty();
            sut.Active.Should().HaveCount(1);
            sut.Active[0].OrderId.Should().Be("ord-2");
        }

        [Fact]
        public async Task CheckOverdueOrdersAsync_AdvancingClockFiresPreviouslyPendingOrder()
        {
            // Start with a future order — nothing fires. Then advance the clock past the
            // completion time; the next check must fire the toast. This proves the watcher
            // re-evaluates the list each tick instead of just evaluating on Track().
            var fakeToast = new FakeToastNotificationService();
            var current = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => current,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder(
                OrderId: "ord-3",
                RefineryName: "MIC-L2 (Long Forest)",
                OreName: "Taranite",
                QuantitySCU: 8m,
                CompleteAtUtc: current.AddMinutes(10)));

            await sut.CheckOverdueOrdersAsync();
            fakeToast.RefineryReadyCalls.Should().BeEmpty();

            // Advance the injected clock past the completion time.
            current = current.AddMinutes(15);

            await sut.CheckOverdueOrdersAsync();
            fakeToast.RefineryReadyCalls.Should().ContainSingle();
            sut.Active.Should().BeEmpty();
        }

        [Fact]
        public void Untrack_RemovesOrderById()
        {
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder("a", "ARC-L1", "Quantanium", 10m, now.AddHours(1)));
            sut.Track(new TrackedRefineryOrder("b", "Levski", "Taranite", 5m, now.AddHours(2)));
            sut.Active.Should().HaveCount(2);

            sut.Untrack("a");

            sut.Active.Should().ContainSingle();
            sut.Active[0].OrderId.Should().Be("b");
        }

        [Fact]
        public void Untrack_UnknownOrderId_IsNoop()
        {
            // Dismissing an id that isn't tracked must not throw — the UI may race dismissal
            // against a natural timer firing.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            Action act = () => sut.Untrack("does-not-exist");

            act.Should().NotThrow();
            sut.Active.Should().BeEmpty();
        }

        [Fact]
        public void Persistence_OrdersRoundTripAcrossInstances()
        {
            // Track a few orders on one instance, dispose it, then construct a second instance
            // pointed at the same persistence file. The second instance must expose the
            // exact same list — this is what lets the watcher survive an app restart.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            var original = new List<TrackedRefineryOrder>
            {
                new("ord-a", "ARC-L1", "Quantanium", 42m, now.AddHours(2)),
                new("ord-b", "Pyro Gateway", "Laranite", 12m, now.AddHours(5)),
                new("ord-c", "Levski", "Taranite", 8.25m, now.AddHours(1)),
            };

            using (var writer = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false))
            {
                foreach (var order in original) writer.Track(order);
            }

            using var reader = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            // Active is sorted by CompleteAtUtc, so compare as sets rather than list order.
            reader.Active.Should().BeEquivalentTo(original);
        }

        [Fact]
        public void Persistence_OnDiskJsonIsRoundTrippable()
        {
            // Locks the serialisation shape: the file must be readable back into the same
            // record shape via plain System.Text.Json. Prevents a future refactor from
            // silently changing the on-disk schema and breaking upgrade-in-place.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            var order = new TrackedRefineryOrder("ord-json", "ARC-L1", "Agricium", 17m, now.AddHours(3));
            sut.Track(order);

            File.Exists(_tempPath).Should().BeTrue();

            string json = File.ReadAllText(_tempPath);
            var parsed = JsonSerializer.Deserialize<List<TrackedRefineryOrder>>(json);

            parsed.Should().NotBeNull();
            parsed!.Should().ContainSingle();
            parsed[0].Should().Be(order);
        }

        [Fact]
        public void Track_SameOrderIdTwice_ReplacesEntry()
        {
            // Re-tracking an id lets the caller update the completion estimate in place
            // without clearing + re-adding.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder("ord-x", "ARC-L1", "Quantanium", 10m, now.AddHours(1)));
            sut.Track(new TrackedRefineryOrder("ord-x", "ARC-L1", "Quantanium", 10m, now.AddHours(3)));

            sut.Active.Should().ContainSingle();
            sut.Active[0].CompleteAtUtc.Should().Be(now.AddHours(3));
        }

        [Fact]
        public async Task CheckOverdueOrdersAsync_MultipleDueOrders_FireAllOfThem()
        {
            // Covers the case where the watcher was asleep for a while (app backgrounded /
            // machine suspended) and wakes with several orders past due.
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            using var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder("a", "ARC-L1", "Quantanium", 10m, now.AddMinutes(-5)));
            sut.Track(new TrackedRefineryOrder("b", "Levski", "Taranite", 5m, now.AddMinutes(-10)));
            sut.Track(new TrackedRefineryOrder("c", "Pyro Gateway", "Laranite", 7m, now.AddHours(3)));

            await sut.CheckOverdueOrdersAsync();

            fakeToast.RefineryReadyCalls.Should().HaveCount(2);
            sut.Active.Should().ContainSingle();
            sut.Active[0].OrderId.Should().Be("c");
        }

        [Fact]
        public async Task DisposeAsync_CleansUpWithoutError()
        {
            var fakeToast = new FakeToastNotificationService();
            var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

            var sut = new RefineryOrderWatcher(
                fakeToast,
                NullLogger<RefineryOrderWatcher>.Instance,
                _tempPath,
                utcNow: () => now,
                startTimer: false);

            sut.Track(new TrackedRefineryOrder("a", "ARC-L1", "Quantanium", 10m, now.AddHours(1)));

            Func<Task> act = async () =>
            {
                await sut.DisposeAsync();
                // Double-dispose must be a safe no-op.
                await sut.DisposeAsync();
            };

            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Test double for <see cref="IToastNotificationService"/> — records every call so
        /// assertions can verify exactly what the watcher would have shown to the user.
        /// No Windows UI is ever touched from tests.
        /// </summary>
        private sealed class FakeToastNotificationService : IToastNotificationService
        {
            public List<(string RefineryName, string OreName, decimal QuantitySCU)> RefineryReadyCalls { get; } = new();
            public List<(string Title, string Message)> InfoCalls { get; } = new();
            public List<(string Title, string Message)> WarningCalls { get; } = new();

            public void ShowRefineryReady(string refineryName, string oreName, decimal quantitySCU)
                => RefineryReadyCalls.Add((refineryName, oreName, quantitySCU));

            public void ShowInfo(string title, string message)
                => InfoCalls.Add((title, message));

            public void ShowWarning(string title, string message)
                => WarningCalls.Add((title, message));
        }
    }
}
