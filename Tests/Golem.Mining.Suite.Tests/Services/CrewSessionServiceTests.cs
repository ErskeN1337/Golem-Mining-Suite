using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Models.Regolith;
using Golem_Mining_Suite.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Wave 5B tests for <see cref="CrewSessionService"/>. Every test uses a unique
    /// temp-file persistence path so xUnit's parallel runner can't trip the file gate.
    /// </summary>
    public class CrewSessionServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public CrewSessionServiceTests()
        {
            _tempPath = Path.Combine(
                Path.GetTempPath(),
                $"golem-crew-sessions-{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
            catch { /* cleanup-best-effort */ }
        }

        // ---------------------------------------------------------------------------
        // Fixture helpers
        // ---------------------------------------------------------------------------

        private CrewSessionService CreateSut() =>
            new(NullLogger<CrewSessionService>.Instance, _tempPath);

        private static ImportedSession MakeSession(
            string id,
            decimal totalAuec = 1_000_000m,
            params (string Handle, decimal Pct)[] crew)
        {
            var members = crew.Length == 0
                ? new List<ImportedCrewMember>
                {
                    new("u1", "Alpha", 0m),
                    new("u2", "Bravo", 0m),
                }
                : crew.Select((c, i) => new ImportedCrewMember($"u{i}", c.Handle, c.Pct)).ToList();

            return new ImportedSession
            {
                Id = id,
                Name = $"Session {id}",
                StartedAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc),
                FinishedAt = null,
                Crew = members,
                WorkOrders = Array.Empty<ImportedWorkOrder>(),
                ScoutingFinds = Array.Empty<RockScan>(),
                TotalPayoutAuec = totalAuec,
                SourceTool = "Regolith",
            };
        }

        // ---------------------------------------------------------------------------
        // Add / remove / persist
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task AddAsync_PersistsSessionAndExposesItThroughSessions()
        {
            using var sut = CreateSut();

            var session = MakeSession("sess-1");

            await sut.AddAsync(session);

            sut.Sessions.Should().ContainSingle();
            sut.Sessions[0].Id.Should().Be("sess-1");
            File.Exists(_tempPath).Should().BeTrue("AddAsync must persist to disk immediately");
        }

        [Fact]
        public async Task AddRangeAsync_SkipsDuplicatesByIdAndPersistsOnce()
        {
            using var sut = CreateSut();

            await sut.AddRangeAsync(new[]
            {
                MakeSession("sess-1"),
                MakeSession("sess-2"),
                MakeSession("sess-1"), // duplicate id — must be ignored
            });

            sut.Sessions.Should().HaveCount(2);
            sut.Sessions.Select(s => s.Id).Should().BeEquivalentTo(new[] { "sess-1", "sess-2" });
        }

        [Fact]
        public async Task AddAsync_DuplicateId_IsSilentNoop()
        {
            using var sut = CreateSut();

            await sut.AddAsync(MakeSession("sess-dupe", totalAuec: 100m));
            await sut.AddAsync(MakeSession("sess-dupe", totalAuec: 999m));

            sut.Sessions.Should().ContainSingle();
            // First add wins — second is ignored, so the total should still be 100.
            sut.Sessions[0].TotalPayoutAuec.Should().Be(100m);
        }

        [Fact]
        public async Task RemoveAsync_RemovesSessionById()
        {
            using var sut = CreateSut();

            await sut.AddAsync(MakeSession("a"));
            await sut.AddAsync(MakeSession("b"));
            await sut.RemoveAsync("a");

            sut.Sessions.Should().ContainSingle();
            sut.Sessions[0].Id.Should().Be("b");
        }

        [Fact]
        public async Task RemoveAsync_UnknownId_IsNoop()
        {
            using var sut = CreateSut();
            await sut.AddAsync(MakeSession("a"));

            Func<Task> act = () => sut.RemoveAsync("does-not-exist");

            await act.Should().NotThrowAsync();
            sut.Sessions.Should().ContainSingle();
        }

        [Fact]
        public async Task LoadAsync_RoundTripsWhatWasWritten()
        {
            // Write on one instance, load on another pointed at the same path.
            using (var writer = CreateSut())
            {
                await writer.AddRangeAsync(new[]
                {
                    MakeSession("r1"),
                    MakeSession("r2"),
                });
            }

            using var reader = CreateSut();
            await reader.LoadAsync();

            reader.Sessions.Select(s => s.Id).Should().BeEquivalentTo(new[] { "r1", "r2" });
        }

        [Fact]
        public async Task LoadAsync_RaisesSessionsChangedOnce()
        {
            using (var writer = CreateSut())
            {
                await writer.AddAsync(MakeSession("r1"));
            }

            using var reader = CreateSut();
            int fires = 0;
            reader.SessionsChanged += (_, _) => fires++;

            await reader.LoadAsync();

            fires.Should().Be(1);
        }

        [Fact]
        public async Task AddAsync_FiresSessionsChangedExactlyOnce()
        {
            using var sut = CreateSut();
            int fires = 0;
            sut.SessionsChanged += (_, _) => fires++;

            await sut.AddAsync(MakeSession("a"));

            fires.Should().Be(1);
        }

        [Fact]
        public async Task AddAsync_DuplicateId_DoesNotFireSessionsChanged()
        {
            using var sut = CreateSut();
            await sut.AddAsync(MakeSession("a"));

            int fires = 0;
            sut.SessionsChanged += (_, _) => fires++;

            await sut.AddAsync(MakeSession("a"));

            fires.Should().Be(0);
        }

        [Fact]
        public async Task Persistence_OnDiskJsonIsPlainSystemTextJson()
        {
            // Locks the serialisation shape — a future refactor that changes the file layout
            // must update this test deliberately rather than silently break upgrade-in-place.
            using var sut = CreateSut();
            await sut.AddAsync(MakeSession("locked", totalAuec: 42m));

            File.Exists(_tempPath).Should().BeTrue();

            string json = File.ReadAllText(_tempPath);
            var parsed = JsonSerializer.Deserialize<List<ImportedSession>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            parsed.Should().NotBeNull();
            parsed!.Should().ContainSingle();
            parsed[0].Id.Should().Be("locked");
            parsed[0].TotalPayoutAuec.Should().Be(42m);
        }

        // ---------------------------------------------------------------------------
        // MyShare math
        // ---------------------------------------------------------------------------

        [Fact]
        public void MyShare_ReturnsPercentageBasedShareWhenHandleIsKnown()
        {
            using var sut = CreateSut();
            var session = MakeSession(
                "pct",
                totalAuec: 1_000_000m,
                ("CaptainGolem", 60m),
                ("AstroAnya", 40m));

            sut.MyShare(session, "CaptainGolem").Should().Be(600_000m);
            sut.MyShare(session, "AstroAnya").Should().Be(400_000m);
        }

        [Fact]
        public void MyShare_HandleMatchIsCaseInsensitive()
        {
            using var sut = CreateSut();
            var session = MakeSession(
                "pct",
                totalAuec: 1_000_000m,
                ("CaptainGolem", 50m),
                ("AstroAnya", 50m));

            sut.MyShare(session, "captaingolem").Should().Be(500_000m);
            sut.MyShare(session, "CAPTAINGOLEM").Should().Be(500_000m);
        }

        [Fact]
        public void MyShare_UnknownHandle_FallsBackToEqualSplit()
        {
            using var sut = CreateSut();
            var session = MakeSession(
                "pct",
                totalAuec: 900_000m,
                ("CaptainGolem", 60m),
                ("AstroAnya", 40m));

            // 3-way equal split fallback is wrong — fallback is over ALL crew, not 3-way.
            // Session has 2 crew, so unknown handle → 900k / 2 = 450k.
            sut.MyShare(session, "Stranger").Should().Be(450_000m);
        }

        [Fact]
        public void MyShare_EmptyHandle_FallsBackToEqualSplit()
        {
            using var sut = CreateSut();
            var session = MakeSession("pct", totalAuec: 900_000m,
                ("Alpha", 60m), ("Bravo", 40m));

            sut.MyShare(session, string.Empty).Should().Be(450_000m);
            sut.MyShare(session, "  ").Should().Be(450_000m);
        }

        [Fact]
        public void MyShare_HandleKnownButNoContributionPct_FallsBackToEqualSplit()
        {
            // Crew loaded from the importer can legitimately have ContributionPct=0 when
            // Regolith only saw AMOUNT/SHARE split types. In that case we should still
            // return the equal-split fallback rather than zero, so the UI isn't misleading.
            using var sut = CreateSut();
            var session = MakeSession("zero", totalAuec: 1_000_000m,
                ("Alpha", 0m), ("Bravo", 0m), ("Charlie", 0m));

            sut.MyShare(session, "Alpha").Should().Be(1_000_000m / 3m);
        }

        [Fact]
        public void MyShare_EmptyCrew_ReturnsZero()
        {
            using var sut = CreateSut();
            var session = new ImportedSession
            {
                Id = "empty",
                Name = "Empty",
                StartedAt = DateTime.UtcNow,
                Crew = Array.Empty<ImportedCrewMember>(),
                WorkOrders = Array.Empty<ImportedWorkOrder>(),
                ScoutingFinds = Array.Empty<RockScan>(),
                TotalPayoutAuec = 500_000m,
                SourceTool = "Regolith",
            };

            sut.MyShare(session, "anyone").Should().Be(0m);
        }

        // ---------------------------------------------------------------------------
        // Concurrency — the SemaphoreSlim must keep the list + file consistent even
        // when many AddAsync calls race.
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ConcurrentAddAsync_DoesNotCorruptStateOrDuplicate()
        {
            using var sut = CreateSut();

            // 100 distinct sessions + 100 racing duplicates of each → the final list must
            // still contain exactly 100 unique entries, and the on-disk file must still
            // parse as a valid JSON array.
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                var session = MakeSession($"sess-{i}");
                tasks.Add(sut.AddAsync(session));
                tasks.Add(sut.AddAsync(session)); // duplicate that must be swallowed
            }

            await Task.WhenAll(tasks);

            sut.Sessions.Should().HaveCount(100);
            sut.Sessions.Select(s => s.Id).Distinct().Should().HaveCount(100);

            // File must be valid JSON even after the blizzard of concurrent writes.
            File.Exists(_tempPath).Should().BeTrue();
            var parsed = JsonSerializer.Deserialize<List<ImportedSession>>(
                File.ReadAllText(_tempPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            parsed.Should().NotBeNull();
            parsed!.Count.Should().Be(100);
        }
    }
}
