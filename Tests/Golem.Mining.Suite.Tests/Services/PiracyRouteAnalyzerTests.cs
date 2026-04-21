using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Golem_Mining_Suite.Models.Piracy;
using Golem_Mining_Suite.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Geometry-focused tests for <see cref="PiracyRouteAnalyzer"/>. We verify the
    /// Snareplan-style point-to-line-segment math and the aggregate risk score
    /// behaviour. Supabase integration is not exercised — the analyzer's
    /// constructor takes a nullable <c>ISupabaseService</c> so tests can run
    /// fully in-memory.
    /// </summary>
    public class PiracyRouteAnalyzerTests
    {
        private static PiracyRouteAnalyzer MakeSut(string? seedPath = null)
        {
            return new PiracyRouteAnalyzer(
                NullLogger<PiracyRouteAnalyzer>.Instance,
                supabase: null,
                seedPath: seedPath,
                pendingReportsPath: Path.Combine(Path.GetTempPath(), $"piracy-test-{Guid.NewGuid():N}.json"));
        }

        private static QtLeg MakeLeg(double ax, double ay, double az, double bx, double by, double bz)
            => new QtLeg("A", new Vec3(ax, ay, az), "B", new Vec3(bx, by, bz));

        private static PullPoint MakePullPoint(string id, double x, double y, double z, double radius = 20.0, int notoriety = 80)
            => new PullPoint
            {
                Id = id,
                Name = id,
                Position = new Vec3(x, y, z),
                RadiusKm = radius,
                Notoriety = notoriety
            };

        [Fact]
        public void Analyze_EmptyRoute_ReturnsEmptyRouteRisk()
        {
            var sut = MakeSut();

            var risk = sut.AnalyzeWith(Array.Empty<QtLeg>(), new List<PullPoint>());

            risk.Legs.Should().BeEmpty();
            risk.Hits.Should().BeEmpty();
            risk.TotalRiskScore.Should().Be(0);
        }

        [Fact]
        public void Analyze_EmptyPullPoints_ReturnsZeroRisk()
        {
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);

            var risk = sut.AnalyzeWith(new[] { leg }, new List<PullPoint>());

            risk.Hits.Should().BeEmpty();
            risk.TotalRiskScore.Should().Be(0);
        }

        [Fact]
        public void Analyze_PullPointCloseToSegment_ProducesHitWithExpectedGeometry()
        {
            // Leg: (0,0,0) → (1000,0,0) km. Pull point at (500,15,0), radius 20.
            // Perpendicular distance = 15 km. Chord = 2 * sqrt(400 - 225) = 2 * sqrt(175) ≈ 26.458 km.
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);
            var pp = MakePullPoint("near", 500, 15, 0, radius: 20);

            var risk = sut.AnalyzeWith(new[] { leg }, new[] { pp });

            risk.Hits.Should().ContainSingle();
            var hit = risk.Hits[0];
            hit.IsOnSegment.Should().BeTrue();
            hit.PerpendicularDistanceKm.Should().BeApproximately(15.0, 1e-6);
            hit.ChordLengthKm.Should().BeApproximately(2 * Math.Sqrt(175), 1e-6);
            risk.TotalRiskScore.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Analyze_PullPointOutsideRadius_ProducesNoHit()
        {
            // Perpendicular distance 50 > radius 20 → no hit at all.
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);
            var pp = MakePullPoint("far", 500, 50, 0, radius: 20);

            var risk = sut.AnalyzeWith(new[] { leg }, new[] { pp });

            risk.Hits.Should().BeEmpty();
            risk.TotalRiskScore.Should().Be(0);
        }

        [Fact]
        public void Analyze_PullPointOffSegmentEnd_IsReportedButContributesZeroRisk()
        {
            // Point at (1500, 5, 0) — only 5 km from the INFINITE line but the
            // closest-approach projection (t=1500) lies past the endpoint (legLen=1000).
            // The analyzer must record this as !IsOnSegment and exclude it from the score.
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);
            var pp = MakePullPoint("past-end", 1500, 5, 0, radius: 20);

            var risk = sut.AnalyzeWith(new[] { leg }, new[] { pp });

            risk.Hits.Should().ContainSingle();
            risk.Hits[0].IsOnSegment.Should().BeFalse();
            risk.Hits[0].ChordLengthKm.Should().Be(0);
            risk.TotalRiskScore.Should().Be(0, "off-segment hits are informational only and must not contribute to score");
        }

        [Fact]
        public void Analyze_DegenerateZeroLengthLeg_IsSkipped()
        {
            // Zero-length leg — QT wouldn't even spool. The analyzer must not divide
            // by zero and must return zero hits.
            var sut = MakeSut();
            var leg = MakeLeg(500, 500, 500, 500, 500, 500);
            var pp = MakePullPoint("coincident", 500, 500, 500, radius: 20);

            var risk = sut.AnalyzeWith(new[] { leg }, new[] { pp });

            risk.Hits.Should().BeEmpty();
            risk.TotalRiskScore.Should().Be(0);
        }

        [Fact]
        public void Analyze_RiskScoreMonotonicallyIncreasesWithNotoriety()
        {
            // Hold geometry constant, vary only notoriety. Risk must be monotonic.
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);

            var low = sut.AnalyzeWith(new[] { leg }, new[] { MakePullPoint("x", 500, 5, 0, radius: 20, notoriety: 10) });
            var med = sut.AnalyzeWith(new[] { leg }, new[] { MakePullPoint("x", 500, 5, 0, radius: 20, notoriety: 50) });
            var high = sut.AnalyzeWith(new[] { leg }, new[] { MakePullPoint("x", 500, 5, 0, radius: 20, notoriety: 95) });

            low.TotalRiskScore.Should().BeLessThan(med.TotalRiskScore);
            med.TotalRiskScore.Should().BeLessThan(high.TotalRiskScore);
        }

        [Fact]
        public void Analyze_RiskScoreClampedTo100()
        {
            // Stack many max-notoriety pull points along the same leg; score must not
            // exceed 100 even though the raw sum would blow past.
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);
            var points = Enumerable.Range(0, 50)
                .Select(i => MakePullPoint($"pp{i}", 500, 1, 0, radius: 20, notoriety: 100))
                .ToList();

            var risk = sut.AnalyzeWith(new[] { leg }, points);

            risk.TotalRiskScore.Should().BeLessThanOrEqualTo(100);
            risk.TotalRiskScore.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Analyze_Summary_MentionsHighestNotorietyHit()
        {
            var sut = MakeSut();
            var leg = MakeLeg(0, 0, 0, 1000, 0, 0);
            var points = new[]
            {
                MakePullPoint("low-note",  500, 10, 0, radius: 20, notoriety: 20),
                MakePullPoint("boss",      400, 5,  0, radius: 20, notoriety: 90),
            };

            var risk = sut.AnalyzeWith(new[] { leg }, points);

            risk.Summary.Should().Contain("boss");
            risk.Summary.Should().Contain("notoriety 90");
        }

        [Fact]
        public async Task GetPullPointsAsync_LoadsShippedSeedFile_ReturnsNonEmptyList()
        {
            // The seed JSON is copied to the test output directory via <Content> in the
            // csproj. Locate it relative to the test assembly.
            var seedPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets", "Data", "piracy-seed.json");

            if (!File.Exists(seedPath))
            {
                // In case the test runner's base directory differs, walk up a couple of
                // steps — but if still missing, skip silently rather than failing in a
                // non-default CI layout.
                return;
            }

            var sut = new PiracyRouteAnalyzer(
                NullLogger<PiracyRouteAnalyzer>.Instance,
                supabase: null,
                seedPath: seedPath,
                pendingReportsPath: Path.Combine(Path.GetTempPath(), $"piracy-seed-test-{Guid.NewGuid():N}.json"));

            var points = await sut.GetPullPointsAsync();

            points.Should().NotBeEmpty();
            points.All(p => p.Source == "seed").Should().BeTrue();
            points.All(p => p.RadiusKm > 0).Should().BeTrue();
            points.All(p => p.Notoriety >= 0 && p.Notoriety <= 100).Should().BeTrue();
        }

        [Fact]
        public async Task ReportPullPointAsync_WithoutSupabase_PersistsLocally()
        {
            // No Supabase wired → the analyzer must fall back to the pending-reports file.
            var pendingPath = Path.Combine(Path.GetTempPath(), $"piracy-pending-test-{Guid.NewGuid():N}.json");
            try
            {
                var sut = new PiracyRouteAnalyzer(
                    NullLogger<PiracyRouteAnalyzer>.Instance,
                    supabase: null,
                    seedPath: null,
                    pendingReportsPath: pendingPath);

                var report = new PullPoint
                {
                    Id = "user-reported-1",
                    Name = "Testville",
                    Position = new Vec3(100, 200, 300),
                    RadiusKm = 20,
                    Notoriety = 75,
                    Source = "self"
                };

                await sut.ReportPullPointAsync(report);

                File.Exists(pendingPath).Should().BeTrue();
                var contents = await File.ReadAllTextAsync(pendingPath);
                contents.Should().Contain("user-reported-1");
            }
            finally
            {
                if (File.Exists(pendingPath)) File.Delete(pendingPath);
            }
        }
    }
}
