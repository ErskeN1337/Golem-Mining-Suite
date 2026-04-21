using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Models.Regolith;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Concrete Regolith importer. Stateless, deterministic, no UI concerns. See
    /// <see cref="IRegolithImporter"/> for the contract.
    /// <para>
    /// Parsing strategy:
    /// <list type="bullet">
    /// <item><description>Top-level walk of the JSON tree with lenient field lookup (case-insensitive).</description></item>
    /// <item><description>BigInt fields accept both <c>"123"</c> and <c>123</c> to match the GraphQL server's
    ///   mixed encoding (see R3 §7.5).</description></item>
    /// <item><description>Missing / malformed fields are recorded as warnings — never thrown — so a single
    ///   corrupt session doesn't torch an entire bulk import.</description></item>
    /// </list>
    /// </para>
    /// TODO (wiring): register in <c>App.xaml.cs</c> once Wave 5C's refinery concurrent work is
    /// merged. Expected setup:
    /// <code>
    /// services.AddHttpClient("regolith", c =>
    /// {
    ///     c.BaseAddress = new Uri("https://api.regolith.rocks");
    ///     c.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// services.AddSingleton&lt;IRegolithImporter, RegolithImporter&gt;();
    /// </code>
    /// </summary>
    public sealed class RegolithImporter : IRegolithImporter
    {
        /// <summary>Named HttpClient key — must match the AddHttpClient registration.</summary>
        public const string HttpClientName = "regolith";

        /// <summary>API key header Regolith expects on every authenticated GraphQL request.</summary>
        internal const string ApiKeyHeader = "x-api-key";

        // Minimal query that fetches only what we map. Keeps us well under the 3,600 req/day cap
        // and avoids paying bandwidth for fields Wave 5B won't use.
        internal const string SessionByIdQuery = @"query PullSession($sid: ID!) {
  session(sessionId: $sid) {
    sessionId joinId ownerId owner { userId scName }
    createdAt updatedAt finishedAt state version name note
    sessionSettings { activity location systemFilter gravityWell }
    mentionedUsers { scName captainId sessionRole shipRole }
    activeMembers { items {
      sessionId ownerId owner { userId scName }
      isPilot sessionRole shipRole captainId shipName state vehicleCode
    } nextToken }
    scouting { items {
      __typename
      ... on ShipClusterFind { scoutingFindId createdAt updatedAt clusterCount ownerId note state gravityWell includeInSurvey score rawScore attendanceIds shipRocks { state mass inst res rockType ores { ore percent } } }
      ... on VehicleClusterFind { scoutingFindId createdAt updatedAt clusterCount ownerId note state gravityWell includeInSurvey score rawScore attendanceIds vehicleRocks { mass inst res ores { ore percent } } }
      ... on SalvageFind { scoutingFindId createdAt updatedAt clusterCount ownerId note state gravityWell includeInSurvey score rawScore attendanceIds wrecks { state isShip shipCode sellableAUEC salvageOres { ore scu } } }
    } nextToken }
    workOrders { items {
      __typename
      ... on ShipMiningOrder { orderId createdAt updatedAt ownerId version state failReason includeTransferFee orderType note isSold shareAmount sellStore shareRefinedValue isRefined processStartTime processDurationS processEndTime refinery method shipOres { ore amt yield } crewShares { payeeScName payeeUserId shareType share state note } }
      ... on VehicleMiningOrder { orderId createdAt updatedAt ownerId version state failReason orderType note isSold shareAmount sellStore vehicleOres { ore amt } crewShares { payeeScName payeeUserId shareType share state note } }
      ... on SalvageOrder { orderId createdAt updatedAt ownerId version state failReason orderType note isSold shareAmount sellStore salvageOres { ore amt } crewShares { payeeScName payeeUserId shareType share state note } }
      ... on OtherOrder { orderId createdAt updatedAt ownerId version state failReason orderType note isSold shareAmount sellStore crewShares { payeeScName payeeUserId shareType share state note } }
    } nextToken }
  }
}";

        internal const string ProfileBootstrapQuery = @"query Me {
  profile {
    userId scName
    mySessions     { items { sessionId } nextToken }
    joinedSessions { items { sessionId } nextToken }
  }
}";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RegolithImporter> _logger;

        public RegolithImporter(IHttpClientFactory httpClientFactory, ILogger<RegolithImporter> logger)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(logger);
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ---------------------------------------------------------------------------------
        // File path
        // ---------------------------------------------------------------------------------

        public async Task<RegolithImportResult> ImportFromFileAsync(string path, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return WarnOnly("Import path was null or empty.");
            }

            if (!File.Exists(path))
            {
                return WarnOnly($"File not found: {path}");
            }

            string json;
            try
            {
                json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to read Regolith export at {Path}", path);
                return WarnOnly($"Could not read file '{path}': {ex.Message}");
            }

            return ParseJsonPayload(json);
        }

        // ---------------------------------------------------------------------------------
        // API paths
        // ---------------------------------------------------------------------------------

        public async Task<RegolithImportResult> ImportFromApiAsync(string apiKey, string sessionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return WarnOnly("API key was null or empty.");
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return WarnOnly("Session id was null or empty.");
            }

            string body;
            try
            {
                body = await PostGraphQlAsync(apiKey, SessionByIdQuery, new { sid = sessionId }, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Regolith GraphQL request failed for session {SessionId}", sessionId);
                return WarnOnly($"Regolith API request failed: {ex.Message}");
            }

            return ParseGraphQlSessionPayload(body);
        }

        public async Task<RegolithImportResult> ImportAllFromApiAsync(string apiKey, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return WarnOnly("API key was null or empty.");
            }

            string bootstrap;
            try
            {
                bootstrap = await PostGraphQlAsync(apiKey, ProfileBootstrapQuery, new { }, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Regolith bootstrap profile query failed");
                return WarnOnly($"Regolith API bootstrap failed: {ex.Message}");
            }

            var sessionIds = ExtractBootstrapSessionIds(bootstrap);
            if (sessionIds.Count == 0)
            {
                return WarnOnly("Regolith profile returned no sessions.");
            }

            var aggregated = new ResultAccumulator();
            int completed = 0;
            foreach (var id in sessionIds)
            {
                ct.ThrowIfCancellationRequested();
                var single = await ImportFromApiAsync(apiKey, id, ct).ConfigureAwait(false);
                aggregated.Merge(single);
                progress?.Report(++completed);
            }

            return aggregated.ToResult();
        }

        // ---------------------------------------------------------------------------------
        // Internals — parsing
        // ---------------------------------------------------------------------------------

        internal RegolithImportResult ParseJsonPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return RegolithImportResult.Empty;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Regolith export was not valid JSON");
                return WarnOnly($"Invalid JSON: {ex.Message}");
            }

            using (doc)
            {
                return ParseSessionFromRoot(doc.RootElement);
            }
        }

        private RegolithImportResult ParseGraphQlSessionPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return WarnOnly("Empty response body from Regolith API.");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Regolith API returned non-JSON");
                return WarnOnly($"Invalid JSON response: {ex.Message}");
            }

            using (doc)
            {
                // GraphQL envelope: { data: { session: {...} }, errors?: [...] }
                var root = doc.RootElement;
                var warnings = new List<string>();
                if (TryGetProp(root, "errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    foreach (var err in errors.EnumerateArray())
                    {
                        if (TryGetProp(err, "message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        {
                            warnings.Add($"GraphQL error: {msg.GetString()}");
                        }
                    }
                }

                JsonElement sessionElem;
                if (TryGetProp(root, "data", out var data)
                    && data.ValueKind == JsonValueKind.Object
                    && TryGetProp(data, "session", out sessionElem)
                    && sessionElem.ValueKind == JsonValueKind.Object)
                {
                    var result = ParseSessionFromRoot(sessionElem);
                    if (warnings.Count == 0)
                    {
                        return result;
                    }

                    var merged = new List<string>(warnings);
                    merged.AddRange(result.Warnings);
                    return result with { Warnings = merged };
                }

                if (warnings.Count == 0)
                {
                    warnings.Add("GraphQL response contained no `data.session` object.");
                }

                return new RegolithImportResult(0, 0, 0, 0m, warnings);
            }
        }

        private RegolithImportResult ParseSessionFromRoot(JsonElement root)
        {
            // Accept three shapes:
            //   1. A raw Session object ({ sessionId, name, ... })
            //   2. A file export wrapped under `{ "session": { ... } }`
            //   3. A GraphQL response data slice (already unwrapped)
            if (root.ValueKind != JsonValueKind.Object)
            {
                return WarnOnly("Top-level JSON was not an object.");
            }

            JsonElement sessionElem = root;
            if (!TryGetProp(root, "sessionId", out _))
            {
                if (TryGetProp(root, "session", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                {
                    sessionElem = wrapped;
                }
                else if (TryGetProp(root, "data", out var data) && data.ValueKind == JsonValueKind.Object
                    && TryGetProp(data, "session", out var gql) && gql.ValueKind == JsonValueKind.Object)
                {
                    sessionElem = gql;
                }
                else
                {
                    return WarnOnly("JSON root did not contain a recognisable `session` object.");
                }
            }

            var warnings = new List<string>();
            var imported = BuildImportedSession(sessionElem, warnings);
            if (imported is null)
            {
                return new RegolithImportResult(0, 0, 0, 0m, warnings);
            }

            return new RegolithImportResult(
                SessionsImported: 1,
                WorkOrdersImported: imported.WorkOrders.Count,
                ScoutingFindsImported: imported.ScoutingFinds.Count,
                TotalAuec: imported.TotalPayoutAuec,
                Warnings: warnings)
            {
                Sessions = new[] { imported },
            };
        }

        private static ImportedSession? BuildImportedSession(JsonElement session, ICollection<string> warnings)
        {
            string? id = GetString(session, "sessionId");
            if (string.IsNullOrWhiteSpace(id))
            {
                warnings.Add("Session is missing `sessionId`; skipping.");
                return null;
            }

            string name = GetString(session, "name") ?? "Untitled Session";
            if (string.IsNullOrWhiteSpace(GetString(session, "name")))
            {
                warnings.Add($"Session '{id}' has no name; using placeholder.");
            }

            DateTime? startedAt = GetEpochMsUtc(session, "createdAt");
            if (startedAt is null)
            {
                warnings.Add($"Session '{id}' missing createdAt; defaulting to epoch 0.");
                startedAt = DateTime.UnixEpoch;
            }

            DateTime? finishedAt = GetEpochMsUtc(session, "finishedAt");

            // -- Crew (active members + pending users) ------------------------------------
            var crew = new List<ImportedCrewMember>();
            if (TryGetProp(session, "activeMembers", out var activeMembers))
            {
                foreach (var member in EnumerateItems(activeMembers))
                {
                    var memberId = GetString(member, "ownerId")
                                   ?? GetNestedString(member, "owner", "userId")
                                   ?? GetNestedString(member, "owner", "scName")
                                   ?? Guid.NewGuid().ToString("N");
                    var handle = GetNestedString(member, "owner", "scName")
                                 ?? GetString(member, "shipName")
                                 ?? "(unknown)";
                    crew.Add(new ImportedCrewMember(memberId, handle, 0m));
                }
            }
            if (TryGetProp(session, "mentionedUsers", out var mentioned) && mentioned.ValueKind == JsonValueKind.Array)
            {
                foreach (var pending in mentioned.EnumerateArray())
                {
                    var handle = GetString(pending, "scName");
                    if (string.IsNullOrWhiteSpace(handle))
                    {
                        warnings.Add($"Session '{id}' has a mentioned user with no scName.");
                        continue;
                    }
                    // pending users use scName as their stable id — userId not yet claimed
                    crew.Add(new ImportedCrewMember(handle, handle, 0m));
                }
            }

            // -- Work orders ---------------------------------------------------------------
            var workOrders = new List<ImportedWorkOrder>();
            decimal totalAuec = 0m;
            if (TryGetProp(session, "workOrders", out var wos))
            {
                foreach (var wo in EnumerateItems(wos))
                {
                    var mapped = MapWorkOrder(wo, warnings, id);
                    if (mapped is null) continue;
                    workOrders.Add(mapped);
                    totalAuec += mapped.SellPrice;
                }
            }

            // -- Scouting finds → RockScan -------------------------------------------------
            var scans = new List<RockScan>();
            if (TryGetProp(session, "scouting", out var scouting))
            {
                foreach (var find in EnumerateItems(scouting))
                {
                    scans.AddRange(MapScoutingFindToScans(find, warnings, id));
                }
            }

            // -- Crew contribution pct (percent-type shares only; AMOUNT/SHARE need prices) -
            ApplyCrewContributions(session, crew, warnings, id);

            return new ImportedSession
            {
                Id = id!,
                Name = name,
                StartedAt = startedAt.Value,
                FinishedAt = finishedAt,
                Crew = crew,
                WorkOrders = workOrders,
                ScoutingFinds = scans,
                TotalPayoutAuec = totalAuec,
                SourceTool = "Regolith",
            };
        }

        private static ImportedWorkOrder? MapWorkOrder(JsonElement wo, ICollection<string> warnings, string sessionId)
        {
            string? orderId = GetString(wo, "orderId");
            if (string.IsNullOrWhiteSpace(orderId))
            {
                warnings.Add($"Session '{sessionId}' has a work order with no orderId; skipping.");
                return null;
            }

            string kind = GetString(wo, "orderType") ?? GetString(wo, "__typename") ?? "OTHER";
            kind = kind switch
            {
                "ShipMiningOrder" => "SHIP_MINING",
                "VehicleMiningOrder" => "VEHICLE_MINING",
                "SalvageOrder" => "SALVAGE",
                "OtherOrder" => "OTHER",
                _ => kind,
            };

            // primary ore = largest amt across ship/vehicle/salvage ore rows (whichever is populated)
            var (oreCode, amount) = PickPrimaryOre(wo);
            decimal sellPrice = GetDecimalLike(wo, "shareAmount") ?? 0m;
            string? refinery = kind == "SHIP_MINING" ? GetString(wo, "refinery") : null;

            return new ImportedWorkOrder(orderId!, kind, oreCode, amount, sellPrice, refinery);
        }

        private static (string OreCode, decimal Amount) PickPrimaryOre(JsonElement wo)
        {
            string bestOre = string.Empty;
            decimal bestAmt = 0m;
            decimal total = 0m;

            void Consider(JsonElement rowsArr, OreEnumKind kind)
            {
                if (rowsArr.ValueKind != JsonValueKind.Array) return;
                foreach (var row in rowsArr.EnumerateArray())
                {
                    var ore = NormaliseOre(GetString(row, "ore"), kind);
                    decimal amt = GetDecimalLike(row, "amt") ?? 0m;
                    total += amt;
                    if (amt > bestAmt)
                    {
                        bestAmt = amt;
                        bestOre = ore;
                    }
                }
            }

            if (TryGetProp(wo, "shipOres", out var ship)) Consider(ship, OreEnumKind.Ship);
            if (TryGetProp(wo, "vehicleOres", out var veh)) Consider(veh, OreEnumKind.Vehicle);
            if (TryGetProp(wo, "salvageOres", out var sal)) Consider(sal, OreEnumKind.Ship); // salvage codes (RMC/CMAT) aren't in our map either way — pass through

            // Amount returned is the aggregate across all rows (Wave 5B wants a single total);
            // OreCode points at the dominant ore. For mixed-ore holds this is a known-lossy
            // projection — documented on ImportedWorkOrder.
            return (bestOre, total);
        }

        private static IEnumerable<RockScan> MapScoutingFindToScans(JsonElement find, ICollection<string> warnings, string sessionId)
        {
            var results = new List<RockScan>();
            // Ship cluster (and also matches the default name used in exports lacking __typename)
            if (TryGetProp(find, "shipRocks", out var shipRocks) && shipRocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var rock in shipRocks.EnumerateArray())
                {
                    var scan = BuildRockScan(rock, valuePercentKey: "percent", oresKey: "ores", warnings, sessionId, oreEnum: OreEnumKind.Ship);
                    if (scan is not null) results.Add(scan);
                }
            }
            if (TryGetProp(find, "vehicleRocks", out var vehRocks) && vehRocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var rock in vehRocks.EnumerateArray())
                {
                    var scan = BuildRockScan(rock, valuePercentKey: "percent", oresKey: "ores", warnings, sessionId, oreEnum: OreEnumKind.Vehicle);
                    if (scan is not null) results.Add(scan);
                }
            }
            // SalvageFind.wrecks is intentionally not mapped to RockScan — wrecks aren't rocks.
            return results;
        }

        private enum OreEnumKind { Ship, Vehicle }

        private static RockScan? BuildRockScan(JsonElement rock, string valuePercentKey, string oresKey,
            ICollection<string> warnings, string sessionId, OreEnumKind oreEnum)
        {
            double massKg = GetDouble(rock, "mass") ?? 0.0;
            // Regolith stores inst/res as floats (spec is ambiguous; values observed in the wild
            // sit in the same 0-100 range as our RockScan so we pass through unchanged).
            double inst = GetDouble(rock, "inst") ?? 0.0;
            double res = GetDouble(rock, "res") ?? 0.0;

            var composition = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (TryGetProp(rock, oresKey, out var ores) && ores.ValueKind == JsonValueKind.Array)
            {
                foreach (var ore in ores.EnumerateArray())
                {
                    var code = NormaliseOre(GetString(ore, "ore"), oreEnum);
                    double pct = GetDouble(ore, valuePercentKey) ?? 0.0;
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    // Regolith stores percent as 0-1; RockScan expects 0-100. Rescale if clearly 0-1.
                    if (pct > 0 && pct <= 1.0) pct *= 100.0;
                    composition[code] = pct;
                }
            }

            if (composition.Count == 0)
            {
                warnings.Add($"Session '{sessionId}' scouting find had a rock with no ore composition; importing with empty map.");
            }

            return new RockScan
            {
                MassKg = massKg,
                Instability = inst,
                Resistance = res,
                EnergyPct = 100.0, // not exposed by Regolith — assume freshly-scanned rocks are fully charged
                Composition = composition,
            };
        }

        private static string NormaliseOre(string? regolithOre, OreEnumKind kind)
        {
            if (string.IsNullOrWhiteSpace(regolithOre)) return string.Empty;
            // Regolith ore enums (e.g. QUANTANIUM) largely align with how users speak about ores,
            // but the rest of Golem keys by UEX short-codes (QUAN, LARA, BEXA, ...). Provide a
            // small mapping for the common cases; fall back to the raw name so the data isn't
            // lost if a 4.7 ore lands that we haven't mapped yet.
            return regolithOre switch
            {
                "QUANTANIUM" => "QUAN",
                "LARANITE" => "LARA",
                "BEXALITE" => "BEXA",
                "TARANITE" => "TARA",
                "HEPHAESTANITE" => "HEPH",
                "AGRICIUM" => "AGRI",
                "TITANIUM" => "TITA",
                "CORUNDUM" => "CORU",
                "TUNGSTEN" => "TUNG",
                "BORASE" => "BORA",
                "DIAMOND" => "DIAM",
                "ALUMINUM" => "ALUM",
                "COPPER" => "COPP",
                "BERYL" => "BERY",
                "QUARTZ" => "QRTZ",
                "GOLD" => "GOLD",
                "IRON" => "IRON",
                "ICE" => "ICE",
                "INERTMATERIAL" => "INERT",
                _ => regolithOre,
            };
        }

        private static void ApplyCrewContributions(JsonElement session, List<ImportedCrewMember> crew, ICollection<string> warnings, string sessionId)
        {
            if (crew.Count == 0) return;
            if (!TryGetProp(session, "workOrders", out var wos)) return;

            // Pct accumulator keyed by scName or userId (whatever the CrewShare carries).
            var pctByKey = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var wo in EnumerateItems(wos))
            {
                if (!TryGetProp(wo, "crewShares", out var shares) || shares.ValueKind != JsonValueKind.Array) continue;
                foreach (var share in shares.EnumerateArray())
                {
                    var type = GetString(share, "shareType");
                    if (!string.Equals(type, "PERCENT", StringComparison.OrdinalIgnoreCase)) continue; // only PERCENT can be aggregated without sale prices
                    var raw = GetDouble(share, "share") ?? 0.0;
                    if (raw <= 0) continue;
                    var key = GetString(share, "payeeUserId") ?? GetString(share, "payeeScName");
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    // Regolith stores PERCENT as 0-1; convert to 0-100.
                    decimal pct = (decimal)raw * 100m;
                    pctByKey[key!] = (pctByKey.TryGetValue(key!, out var existing) ? existing : 0m) + pct;
                }
            }

            if (pctByKey.Count == 0) return;

            // Normalise against the number of work orders so a 30% share across 3 orders reads
            // as "30% of the session" (average), not "90%". This is best-effort; CrewSessionService
            // will re-derive once we wire it to a price feed.
            int workOrderCount = 0;
            foreach (var _ in EnumerateItems(GetPropOrDefault(session, "workOrders"))) workOrderCount++;
            if (workOrderCount > 1)
            {
                foreach (var k in new List<string>(pctByKey.Keys))
                {
                    pctByKey[k] = Math.Round(pctByKey[k] / workOrderCount, 2, MidpointRounding.AwayFromZero);
                }
            }

            for (int i = 0; i < crew.Count; i++)
            {
                var m = crew[i];
                if (pctByKey.TryGetValue(m.Id, out var pctById))
                {
                    crew[i] = new ImportedCrewMember(m.Id, m.Handle, pctById);
                }
                else if (pctByKey.TryGetValue(m.Handle, out var pctByHandle))
                {
                    crew[i] = new ImportedCrewMember(m.Id, m.Handle, pctByHandle);
                }
            }

            // Surface a warning if we saw share rows we couldn't match to any crew member — the
            // payee likely had their Regolith account deleted.
            foreach (var kv in pctByKey)
            {
                bool matched = false;
                foreach (var m in crew)
                {
                    if (string.Equals(m.Id, kv.Key, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(m.Handle, kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    warnings.Add($"Session '{sessionId}' had crew share for unknown payee '{kv.Key}'.");
                }
            }
        }

        // ---------------------------------------------------------------------------------
        // Internals — HTTP
        // ---------------------------------------------------------------------------------

        private async Task<string> PostGraphQlAsync(string apiKey, string query, object variables, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, (Uri?)null);
            if (client.BaseAddress is null)
            {
                request.RequestUri = new Uri("https://api.regolith.rocks");
            }

            request.Headers.TryAddWithoutValidation(ApiKeyHeader, apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = JsonSerializer.Serialize(new { query, variables }, JsonOptions);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                // Surface the body verbatim up the call chain so warnings are specific.
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {body}");
            }

            return body;
        }

        private List<string> ExtractBootstrapSessionIds(string json)
        {
            var ids = new List<string>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!TryGetProp(doc.RootElement, "data", out var data)) return ids;
                if (!TryGetProp(data, "profile", out var profile)) return ids;
                foreach (var key in new[] { "mySessions", "joinedSessions" })
                {
                    if (TryGetProp(profile, key, out var paginated))
                    {
                        foreach (var session in EnumerateItems(paginated))
                        {
                            var sid = GetString(session, "sessionId");
                            if (!string.IsNullOrWhiteSpace(sid)) ids.Add(sid!);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Bootstrap profile JSON could not be parsed.");
            }
            return ids;
        }

        // ---------------------------------------------------------------------------------
        // Internals — JSON helpers (lenient)
        // ---------------------------------------------------------------------------------

        private static bool TryGetProp(JsonElement elem, string name, out JsonElement value)
        {
            if (elem.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            // System.Text.Json's default lookup is case-sensitive; iterate once to allow
            // either `createdAt` or `CreatedAt`.
            foreach (var prop in elem.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static JsonElement GetPropOrDefault(JsonElement elem, string name)
            => TryGetProp(elem, name, out var v) ? v : default;

        private static string? GetString(JsonElement elem, string name)
        {
            if (!TryGetProp(elem, name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => v.GetRawText(),
            };
        }

        private static string? GetNestedString(JsonElement elem, string outer, string inner)
            => TryGetProp(elem, outer, out var o) ? GetString(o, inner) : null;

        private static double? GetDouble(JsonElement elem, string name)
        {
            if (!TryGetProp(elem, name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return null;
        }

        private static decimal? GetDecimalLike(JsonElement elem, string name)
        {
            // BigInt values land as numbers or strings depending on the server — accept both.
            if (!TryGetProp(elem, name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number)
            {
                if (v.TryGetDecimal(out var dec)) return dec;
                if (v.TryGetDouble(out var dd)) return (decimal)dd;
            }
            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        private static DateTime? GetEpochMsUtc(JsonElement elem, string name)
        {
            if (!TryGetProp(elem, name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var ms))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }
            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            }
            return null;
        }

        /// <summary>
        /// Iterate either a raw JSON array OR a Regolith paginated wrapper
        /// (<c>{ "items": [...], "nextToken": "..." }</c>). Non-collection inputs yield nothing.
        /// </summary>
        private static IEnumerable<JsonElement> EnumerateItems(JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in elem.EnumerateArray())
                {
                    yield return item;
                }
                yield break;
            }
            if (elem.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProp(elem, "items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        yield return item;
                    }
                }
            }
        }

        private static RegolithImportResult WarnOnly(string warning) =>
            new RegolithImportResult(0, 0, 0, 0m, new[] { warning });

        /// <summary>
        /// Helper for accumulating a multi-session bulk import.
        /// </summary>
        private sealed class ResultAccumulator
        {
            private int _sessions, _workOrders, _finds;
            private decimal _totalAuec;
            private readonly List<string> _warnings = new();
            private readonly List<ImportedSession> _sessionsList = new();

            public void Merge(RegolithImportResult r)
            {
                _sessions += r.SessionsImported;
                _workOrders += r.WorkOrdersImported;
                _finds += r.ScoutingFindsImported;
                _totalAuec += r.TotalAuec;
                _warnings.AddRange(r.Warnings);
                _sessionsList.AddRange(r.Sessions);
            }

            public RegolithImportResult ToResult() =>
                new RegolithImportResult(_sessions, _workOrders, _finds, _totalAuec, _warnings)
                {
                    Sessions = _sessionsList,
                };
        }
    }
}
