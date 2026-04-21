using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Golem.Mining.Suite.Tests.Helpers;
using Golem_Mining_Suite.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Wave 5A tests for <see cref="RegolithImporter"/>. Pins the file-drop JSON parser and the
    /// GraphQL API path behaviour (request headers + response handling) so the migration
    /// experience doesn't regress as we chase the 2026-06-01 shutdown.
    /// </summary>
    public class RegolithImporterTests
    {
        // A realistic Regolith per-session JSON export. Shapes match the types in
        // tasks/research/_regolith_schema/*.gql:
        //   - sessions.gql (Session, SessionUser, PendingUser, SessionSettings)
        //   - workorders.gql (ShipMiningOrder, VehicleMiningOrder, union WorkOrder)
        //   - scouting.gql (ShipClusterFind, ShipRock)
        //   - crewshares.gql (CrewShare, ShareTypeEnum)
        // Regolith uses epoch-ms integers for Timestamp and mixed string/int for BigInt — both
        // variants appear below to prove the lenient numeric parsing.
        private const string SampleSessionJson = """
        {
          "sessionId": "sess-001",
          "joinId": "join-abc",
          "ownerId": "user-owner",
          "owner": { "userId": "user-owner", "scName": "CaptainGolem" },
          "createdAt": 1711000000000,
          "updatedAt": 1711003600000,
          "finishedAt": 1711007200000,
          "state": "CLOSED",
          "version": "4.3.1",
          "name": "Friday Night Mole Run",
          "note": "Yela belt sweep",
          "sessionSettings": {
            "activity": "SHIP_MINING",
            "location": "SPACE",
            "systemFilter": "STANTON",
            "gravityWell": "STANTON.YELA"
          },
          "mentionedUsers": [
            { "scName": "PendingPal", "sessionRole": "SCOUT", "shipRole": "COPILOT" }
          ],
          "activeMembers": {
            "items": [
              { "sessionId": "sess-001", "ownerId": "user-owner", "owner": { "userId": "user-owner", "scName": "CaptainGolem" }, "isPilot": true, "sessionRole": "MANAGER", "shipRole": "PILOT", "shipName": "Molissa", "state": "ON_SITE", "vehicleCode": "MOLE" },
              { "sessionId": "sess-001", "ownerId": "user-crew", "owner": { "userId": "user-crew", "scName": "AstroAnya" }, "isPilot": false, "sessionRole": "LOGISTICS", "shipRole": "LASER_OPERATOR", "captainId": "user-owner", "state": "ON_SITE" }
            ],
            "nextToken": null
          },
          "workOrders": {
            "items": [
              {
                "__typename": "ShipMiningOrder",
                "orderId": "wo-ship-01",
                "sessionId": "sess-001",
                "ownerId": "user-owner",
                "version": "4.3.1",
                "createdAt": 1711000500000,
                "updatedAt": 1711003500000,
                "state": "DONE",
                "orderType": "SHIP_MINING",
                "includeTransferFee": true,
                "isSold": true,
                "shareAmount": "1200000",
                "sellStore": "CRU-L1",
                "refinery": "CRUL1",
                "method": "DINYX_SOLVENTATION",
                "shareRefinedValue": true,
                "isRefined": true,
                "processStartTime": 1711001000000,
                "processDurationS": 3600,
                "shipOres": [
                  { "ore": "QUANTANIUM", "amt": 120, "yield": 84 },
                  { "ore": "LARANITE",   "amt": 40,  "yield": 28 }
                ],
                "crewShares": [
                  { "payeeScName": "CaptainGolem", "payeeUserId": "user-owner", "shareType": "PERCENT", "share": 0.6, "state": true },
                  { "payeeScName": "AstroAnya",    "payeeUserId": "user-crew",  "shareType": "PERCENT", "share": 0.4, "state": true }
                ]
              },
              {
                "__typename": "VehicleMiningOrder",
                "orderId": "wo-veh-02",
                "sessionId": "sess-001",
                "ownerId": "user-crew",
                "version": "4.3.1",
                "createdAt": 1711002000000,
                "updatedAt": 1711004000000,
                "state": "DONE",
                "orderType": "VEHICLE_MINING",
                "isSold": true,
                "shareAmount": 250000,
                "vehicleOres": [
                  { "ore": "HADANITE", "amt": 32 }
                ],
                "crewShares": [
                  { "payeeScName": "AstroAnya", "payeeUserId": "user-crew", "shareType": "PERCENT", "share": 1.0, "state": false }
                ]
              },
              {
                "__typename": "SalvageOrder",
                "orderId": "wo-sal-03",
                "sessionId": "sess-001",
                "ownerId": "user-owner",
                "version": "4.3.1",
                "createdAt": 1711002500000,
                "updatedAt": 1711004500000,
                "state": "REFINING_COMPLETE",
                "orderType": "SALVAGE",
                "isSold": false,
                "shareAmount": 0,
                "salvageOres": [
                  { "ore": "RMC", "amt": 12 }
                ],
                "crewShares": []
              }
            ],
            "nextToken": null
          },
          "scouting": {
            "items": [
              {
                "__typename": "ShipClusterFind",
                "scoutingFindId": "scan-01",
                "sessionId": "sess-001",
                "ownerId": "user-owner",
                "createdAt": 1711000100000,
                "updatedAt": 1711000200000,
                "clusterType": "SHIP",
                "version": "4.3.1",
                "clusterCount": 3,
                "state": "READY_FOR_WORKERS",
                "gravityWell": "STANTON.YELA",
                "includeInSurvey": true,
                "score": 812,
                "rawScore": 790,
                "attendanceIds": ["user-owner", "user-crew"],
                "shipRocks": [
                  {
                    "state": "READY",
                    "mass": 4200.5,
                    "inst": 12.5,
                    "res": 44.1,
                    "rockType": "CTYPE",
                    "ores": [
                      { "ore": "QUANTANIUM", "percent": 0.18 },
                      { "ore": "LARANITE",   "percent": 0.22 },
                      { "ore": "INERTMATERIAL", "percent": 0.60 }
                    ]
                  }
                ]
              }
            ],
            "nextToken": null
          }
        }
        """;

        private static RegolithImporter CreateSut(IHttpClientFactory? factory = null) =>
            new RegolithImporter(factory ?? StubHttpClientFactory.FromResponse("{}"), NullLogger<RegolithImporter>.Instance);

        // -----------------------------------------------------------------------------
        // File-path tests
        // -----------------------------------------------------------------------------

        [Fact]
        public async Task ImportFromFileAsync_SampleSession_RoundTripsIdNameCrewAndTotals()
        {
            var path = await WriteTempAsync(SampleSessionJson);
            try
            {
                var sut = CreateSut();

                var result = await sut.ImportFromFileAsync(path);

                result.Should().NotBeNull();
                result.SessionsImported.Should().Be(1);
                result.WorkOrdersImported.Should().Be(3);
                result.ScoutingFindsImported.Should().Be(1);
                // TotalAuec = sum of shareAmount across all work orders: 1,200,000 + 250,000 + 0
                result.TotalAuec.Should().Be(1_450_000m);

                var session = result.Sessions.Should().ContainSingle().Subject;
                session.Id.Should().Be("sess-001");
                session.Name.Should().Be("Friday Night Mole Run");
                session.SourceTool.Should().Be("Regolith");

                // 2 active members + 1 pending user
                session.Crew.Should().HaveCount(3);
                session.Crew.Select(c => c.Handle).Should().Contain(new[] { "CaptainGolem", "AstroAnya", "PendingPal" });

                // Work orders: ShipMining, VehicleMining, Salvage
                session.WorkOrders.Should().HaveCount(3);
                session.WorkOrders.Select(w => w.Kind).Should().BeEquivalentTo(new[] { "SHIP_MINING", "VEHICLE_MINING", "SALVAGE" });

                // Ship order's dominant ore should be QUAN (120 amt vs 40 for LARA)
                var shipOrder = session.WorkOrders.Single(w => w.Id == "wo-ship-01");
                shipOrder.OreCode.Should().Be("QUAN");
                shipOrder.Amount.Should().Be(160m); // 120 + 40
                shipOrder.SellPrice.Should().Be(1_200_000m);
                shipOrder.Refinery.Should().Be("CRUL1");

                // Scouting find yields one RockScan with 3 ores in its composition.
                session.ScoutingFinds.Should().ContainSingle();
                var scan = session.ScoutingFinds[0];
                scan.MassKg.Should().Be(4200.5);
                scan.Composition.Should().HaveCount(3);
                // 0.18 gets rescaled from 0-1 to 0-100
                scan.Composition["QUAN"].Should().BeApproximately(18.0, 0.001);
                scan.Composition["LARA"].Should().BeApproximately(22.0, 0.001);
                scan.Composition["INERT"].Should().BeApproximately(60.0, 0.001);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportFromFileAsync_MissingFields_RecordsWarningWithoutThrowing()
        {
            // Session with sessionId but no name, no activeMembers, no createdAt, and a work order
            // missing its orderId (to trigger multiple warning paths).
            const string json = """
            {
              "sessionId": "sess-partial",
              "state": "ACTIVE",
              "workOrders": {
                "items": [
                  { "__typename": "OtherOrder", "orderType": "OTHER" }
                ]
              }
            }
            """;
            var path = await WriteTempAsync(json);
            try
            {
                var sut = CreateSut();

                var result = await sut.ImportFromFileAsync(path);

                result.SessionsImported.Should().Be(1, "a session with a sessionId is still importable");
                result.Warnings.Should().NotBeEmpty();
                result.Warnings.Should().Contain(w => w.Contains("no name", StringComparison.OrdinalIgnoreCase));
                result.Warnings.Should().Contain(w => w.Contains("createdAt", StringComparison.OrdinalIgnoreCase));
                result.Warnings.Should().Contain(w => w.Contains("orderId", StringComparison.OrdinalIgnoreCase));
                // Work order was dropped but the session itself imported.
                result.WorkOrdersImported.Should().Be(0);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportFromFileAsync_EmptyFile_ReturnsEmptyResult()
        {
            var path = await WriteTempAsync(string.Empty);
            try
            {
                var sut = CreateSut();

                var result = await sut.ImportFromFileAsync(path);

                result.Should().NotBeNull();
                result.SessionsImported.Should().Be(0);
                result.WorkOrdersImported.Should().Be(0);
                result.ScoutingFindsImported.Should().Be(0);
                result.TotalAuec.Should().Be(0m);
                result.Sessions.Should().BeEmpty();
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public async Task ImportFromFileAsync_MissingFile_ReturnsWarning()
        {
            var sut = CreateSut();
            var result = await sut.ImportFromFileAsync(@"C:\regolith-not-a-real-file.json");

            result.SessionsImported.Should().Be(0);
            result.Warnings.Should().ContainSingle().Which.Should().Contain("File not found");
        }

        // -----------------------------------------------------------------------------
        // GraphQL API path tests
        // -----------------------------------------------------------------------------

        [Fact]
        public async Task ImportFromApiAsync_PostsGraphQlQueryAndApiKeyHeader()
        {
            // Canned GraphQL envelope wrapping the sample session.
            var envelope = $"{{ \"data\": {{ \"session\": {SampleSessionJson} }} }}";

            var handler = new CapturingHandler(envelope);
            var factory = new StubHttpClientFactory(handler);
            var sut = new RegolithImporter(factory, NullLogger<RegolithImporter>.Instance);

            var result = await sut.ImportFromApiAsync("test-api-key-xyz", "sess-001");

            // Verify the request shape
            handler.LastRequest.Should().NotBeNull();
            handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
            handler.LastRequest.Headers.TryGetValues("x-api-key", out var apiKeyHeaders).Should().BeTrue();
            apiKeyHeaders!.Should().ContainSingle().Which.Should().Be("test-api-key-xyz");
            handler.LastRequestBody.Should().Contain("query PullSession");
            handler.LastRequestBody.Should().Contain("sess-001", "the session id must round-trip in the variables block");
            handler.LastRequestBody.Should().Contain("\"variables\"");

            // Verify the response was parsed as if it were a file import.
            result.SessionsImported.Should().Be(1);
            result.Sessions.Should().ContainSingle().Which.Id.Should().Be("sess-001");
            result.TotalAuec.Should().Be(1_450_000m);
        }

        [Fact]
        public async Task ImportFromApiAsync_GraphQlErrors_SurfaceAsWarnings()
        {
            const string errorBody = """
            { "data": null, "errors": [ { "message": "Not authorized" } ] }
            """;
            var factory = new StubHttpClientFactory(new CapturingHandler(errorBody));
            var sut = new RegolithImporter(factory, NullLogger<RegolithImporter>.Instance);

            var result = await sut.ImportFromApiAsync("bad-key", "whatever");

            result.SessionsImported.Should().Be(0);
            result.Warnings.Should().Contain(w => w.Contains("Not authorized", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ImportFromApiAsync_MissingApiKey_ReturnsWarningWithoutNetworkCall()
        {
            var handler = new CapturingHandler("should-never-be-returned");
            var factory = new StubHttpClientFactory(handler);
            var sut = new RegolithImporter(factory, NullLogger<RegolithImporter>.Instance);

            var result = await sut.ImportFromApiAsync("   ", "sess-001");

            result.SessionsImported.Should().Be(0);
            result.Warnings.Should().ContainSingle().Which.Should().Contain("API key");
            handler.LastRequest.Should().BeNull("no HTTP call should go out when the API key is blank");
        }

        // -----------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------

        private static async Task<string> WriteTempAsync(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"regolith-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, content);
            return path;
        }

        /// <summary>
        /// HttpMessageHandler that captures the last request for assertion and replies with a
        /// fixed body. Kept local to this test class so the shared
        /// <see cref="StubHttpClientFactory"/> stays simple.
        /// </summary>
        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly string _body;
            public HttpRequestMessage? LastRequest { get; private set; }
            public string LastRequestBody { get; private set; } = string.Empty;

            public CapturingHandler(string body) { _body = body; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (request.Content is not null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json"),
                };
            }
        }
    }
}
