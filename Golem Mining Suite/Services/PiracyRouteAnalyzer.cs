using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models.Piracy;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Concrete counter-piracy route analyzer. Pure geometry (no WPF / UI
    /// dependencies) so the service is trivially unit-testable. See
    /// <see cref="IPiracyRouteAnalyzer"/> for the contract and R4 §1 for the
    /// math.
    /// </summary>
    public sealed class PiracyRouteAnalyzer : IPiracyRouteAnalyzer
    {
        private readonly ILogger<PiracyRouteAnalyzer> _logger;
        private readonly ISupabaseService? _supabase;
        private readonly string _seedPath;
        private readonly string _pendingReportsPath;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(10);
        private readonly SemaphoreSlim _cacheLock = new(1, 1);

        private IReadOnlyList<PullPoint>? _cached;
        private DateTime _cachedAtUtc = DateTime.MinValue;

        /// <summary>
        /// Default constructor used at runtime. Both the Supabase service and the
        /// filesystem paths are optional so this analyzer can run headless in tests.
        /// </summary>
        public PiracyRouteAnalyzer(
            ILogger<PiracyRouteAnalyzer> logger,
            ISupabaseService? supabase = null,
            string? seedPath = null,
            string? pendingReportsPath = null)
        {
            _logger = logger;
            _supabase = supabase;
            _seedPath = seedPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "Data", "piracy-seed.json");
            _pendingReportsPath = pendingReportsPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Golem Mining Suite",
                "pending-piracy-reports.json");
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PullPoint>> GetPullPointsAsync(CancellationToken ct = default)
        {
            await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_cached != null && DateTime.UtcNow - _cachedAtUtc < _cacheTtl)
                    return _cached;

                var seed = LoadSeed();
                IReadOnlyList<PullPoint> crowd = Array.Empty<PullPoint>();
                if (_supabase != null)
                {
                    try
                    {
                        crowd = await _supabase.GetCrowdsourcedPullPointsAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Crowdsourced pull-point fetch failed; falling back to seed only.");
                    }
                }

                // De-duplicate by Id — crowdsourced wins over seed when both exist.
                var merged = new Dictionary<string, PullPoint>(StringComparer.Ordinal);
                foreach (var p in seed) merged[p.Id] = p;
                foreach (var p in crowd) merged[p.Id] = p;

                _cached = merged.Values.ToList();
                _cachedAtUtc = DateTime.UtcNow;
                return _cached;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public RouteRisk Analyze(IEnumerable<QtLeg> route)
        {
            ArgumentNullException.ThrowIfNull(route);
            var legs = route.ToList();
            var points = _cached ?? LoadSeed();

            return AnalyzeInternal(legs, points);
        }

        /// <summary>
        /// Test hook: analyze a route against a caller-supplied pull-point list.
        /// Keeps the public <see cref="Analyze"/> signature minimal while letting
        /// unit tests inject precise geometry without touching the seed file.
        /// </summary>
        internal RouteRisk AnalyzeWith(IEnumerable<QtLeg> route, IReadOnlyList<PullPoint> pullPoints)
        {
            ArgumentNullException.ThrowIfNull(route);
            ArgumentNullException.ThrowIfNull(pullPoints);
            return AnalyzeInternal(route.ToList(), pullPoints);
        }

        private static RouteRisk AnalyzeInternal(IReadOnlyList<QtLeg> legs, IReadOnlyList<PullPoint> points)
        {
            if (legs.Count == 0 || points.Count == 0)
            {
                return new RouteRisk
                {
                    Legs = legs,
                    Hits = Array.Empty<PullPointHit>(),
                    TotalRiskScore = 0,
                    Summary = legs.Count == 0
                        ? "No route supplied."
                        : "No known pull-points — analysis returned zero hits."
                };
            }

            var hits = new List<PullPointHit>();
            double score = 0;

            foreach (var leg in legs)
            {
                var legVec = leg.To.Minus(leg.From);
                var legLen = legVec.Magnitude();

                // Degenerate leg (identical endpoints) — no QT spool, not interdictable.
                // Skip rather than divide by zero.
                if (legLen <= 0.0)
                    continue;

                var u = new Vec3(legVec.X / legLen, legVec.Y / legLen, legVec.Z / legLen);

                foreach (var pp in points)
                {
                    var w = pp.Position.Minus(leg.From);
                    var t = w.Dot(u); // projection along the leg, in km
                    var closest = leg.From.Plus(new Vec3(u.X * t, u.Y * t, u.Z * t));
                    var d = pp.Position.Minus(closest).Magnitude();

                    var isOnSegment = t >= 0.0 && t <= legLen;

                    if (d >= pp.RadiusKm)
                        continue; // outside snare sphere entirely

                    if (!isOnSegment)
                    {
                        // Sphere brushes an infinite line but the QT segment's endpoints
                        // pass outside it — record for diagnostic completeness but do
                        // NOT contribute to risk.
                        hits.Add(new PullPointHit(
                            leg,
                            pp,
                            PerpendicularDistanceKm: d,
                            ChordLengthKm: 0,
                            IsOnSegment: false));
                        continue;
                    }

                    // Chord through the sphere along an infinite line. We conservatively
                    // clamp the reported chord to the segment's actual length so a sphere
                    // straddling an endpoint doesn't inflate risk past the real exposure.
                    var halfChord = Math.Sqrt(Math.Max(0, pp.RadiusKm * pp.RadiusKm - d * d));
                    var chordStart = Math.Max(0, t - halfChord);
                    var chordEnd = Math.Min(legLen, t + halfChord);
                    var chord = Math.Max(0, chordEnd - chordStart);

                    hits.Add(new PullPointHit(
                        leg,
                        pp,
                        PerpendicularDistanceKm: d,
                        ChordLengthKm: chord,
                        IsOnSegment: true));

                    // Risk contribution: notoriety fraction × depth-of-penetration × chord-fraction.
                    // Each factor is in [0, 1]; product is at most 1 per hit.
                    var notorietyFrac = Math.Clamp(pp.Notoriety / 100.0, 0, 1);
                    var penetrationFrac = 1.0 - (d / pp.RadiusKm);
                    var chordFrac = chord / legLen;
                    score += notorietyFrac * penetrationFrac * chordFrac;
                }
            }

            // Scale contribution so a single near-direct hit on a notoriety-100 pull
            // point (chord ≈ 2r on a short leg) lands near 100. Empirical factor: 100×.
            var totalRisk = Math.Clamp(score * 100.0, 0, 100);

            return new RouteRisk
            {
                Legs = legs,
                Hits = hits,
                TotalRiskScore = totalRisk,
                Summary = BuildSummary(legs, hits, totalRisk)
            };
        }

        private static string BuildSummary(IReadOnlyList<QtLeg> legs, IReadOnlyList<PullPointHit> hits, double totalRisk)
        {
            var scoring = hits.Where(h => h.IsOnSegment).ToList();
            if (scoring.Count == 0)
                return $"No pull-point hits across {legs.Count} leg{(legs.Count == 1 ? "" : "s")}.";

            var worst = scoring
                .OrderByDescending(h => h.PullPoint.Notoriety)
                .ThenBy(h => h.PerpendicularDistanceKm)
                .First();

            var legsHit = scoring.Select(h => h.Leg).Distinct().Count();

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} hit{1} across {2} leg{3} — highest risk at '{4}' (notoriety {5}, {6:F0} km from QT line). Total risk {7:F0}/100.",
                scoring.Count,
                scoring.Count == 1 ? "" : "s",
                legsHit,
                legsHit == 1 ? "" : "s",
                worst.PullPoint.Name,
                worst.PullPoint.Notoriety,
                worst.PerpendicularDistanceKm,
                totalRisk);
        }

        /// <inheritdoc />
        public async Task ReportPullPointAsync(PullPoint point, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(point);

            var enriched = point with
            {
                LastReportedAt = point.LastReportedAt ?? DateTime.UtcNow,
                Source = point.Source == "seed" ? "self" : point.Source
            };

            // Invalidate cache so the next GetPullPointsAsync picks up any fresh
            // community data (and, indirectly, our own upload once aggregated).
            await _cacheLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _cached = null;
            }
            finally
            {
                _cacheLock.Release();
            }

            if (_supabase != null)
            {
                try
                {
                    var ok = await _supabase.UploadPullPointReportAsync(enriched).ConfigureAwait(false);
                    if (ok) return;
                    _logger.LogWarning("Supabase refused pull-point report {Id}; persisting locally for retry.", enriched.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Supabase pull-point upload threw; persisting locally for retry.");
                }
            }
            else
            {
                _logger.LogWarning("Supabase not configured; persisting pull-point report {Id} locally.", enriched.Id);
            }

            await PersistPendingAsync(enriched, ct).ConfigureAwait(false);
        }

        private async Task PersistPendingAsync(PullPoint point, CancellationToken ct)
        {
            try
            {
                var dir = Path.GetDirectoryName(_pendingReportsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var existing = new List<PullPoint>();
                if (File.Exists(_pendingReportsPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(_pendingReportsPath, ct).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var decoded = JsonSerializer.Deserialize<List<PullPoint>>(json);
                            if (decoded != null) existing = decoded;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not read existing pending-piracy-reports.json; starting fresh.");
                    }
                }

                existing.Add(point);
                var payload = JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_pendingReportsPath, payload, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist pending pull-point report to {Path}", _pendingReportsPath);
            }
        }

        private IReadOnlyList<PullPoint> LoadSeed()
        {
            try
            {
                if (!File.Exists(_seedPath))
                {
                    _logger.LogWarning("piracy-seed.json not found at {Path}; analyzer will run with empty seed.", _seedPath);
                    return Array.Empty<PullPoint>();
                }

                var json = File.ReadAllText(_seedPath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("pull_points", out var ppArray) ||
                    ppArray.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("piracy-seed.json missing 'pull_points' array at {Path}.", _seedPath);
                    return Array.Empty<PullPoint>();
                }

                var result = new List<PullPoint>();
                foreach (var el in ppArray.EnumerateArray())
                {
                    var pp = ParseSeedEntry(el);
                    if (pp != null) result.Add(pp);
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load piracy seed from {Path}", _seedPath);
                return Array.Empty<PullPoint>();
            }
        }

        private static PullPoint? ParseSeedEntry(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;

            string? id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString() : null;
            string? name = el.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                return null;

            double x = 0, y = 0, z = 0;
            if (el.TryGetProperty("position_km", out var posEl) && posEl.ValueKind == JsonValueKind.Object)
            {
                if (posEl.TryGetProperty("x", out var xe) && xe.TryGetDouble(out var xv)) x = xv;
                if (posEl.TryGetProperty("y", out var ye) && ye.TryGetDouble(out var yv)) y = yv;
                if (posEl.TryGetProperty("z", out var ze) && ze.TryGetDouble(out var zv)) z = zv;
            }

            double radius = 20.0;
            if (el.TryGetProperty("radius_km", out var rEl) && rEl.TryGetDouble(out var rv) && rv > 0) radius = rv;

            int notoriety = 50;
            if (el.TryGetProperty("notoriety", out var nEl) && nEl.TryGetInt32(out var nv)) notoriety = Math.Clamp(nv, 0, 100);

            string source = "seed";
            if (el.TryGetProperty("source", out var sEl) && sEl.ValueKind == JsonValueKind.String)
                source = sEl.GetString() ?? "seed";

            return new PullPoint
            {
                Id = id!,
                Name = name!,
                Position = new Vec3(x, y, z),
                RadiusKm = radius,
                Notoriety = notoriety,
                Source = source
            };
        }
    }
}
