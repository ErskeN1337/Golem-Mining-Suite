using FluentAssertions;
using Golem.Mining.Suite.Tests.Helpers;
using Golem_Mining_Suite.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    /// <summary>
    /// Pre-4.7 baseline tests for RefineryService. These lock current behavior so Wave 4's
    /// refactor cannot silently regress yield/cost math or the fallback path.
    /// </summary>
    public class RefineryServiceTests
    {
        // Minimal JSON shaped like UEX's /2.0/refineries_methods endpoint — one entry per
        // in-game refinery method. rating_yield / rating_cost / rating_speed map to the
        // service's percent bands (see RefineryService.cs lines 49-50).
        private const string UexNineMethodResponse = """
        {
          "data": [
            { "name": "Cormack",                 "code": "CORMACK",      "rating_yield": 2, "rating_cost": 2, "rating_speed": 2 },
            { "name": "Dinyx Solventation",      "code": "DINYX",        "rating_yield": 3, "rating_cost": 3, "rating_speed": 1 },
            { "name": "Electrostarolysis",       "code": "ELECTRO",      "rating_yield": 2, "rating_cost": 2, "rating_speed": 2 },
            { "name": "Ferron Exchange",         "code": "FERRON",       "rating_yield": 1, "rating_cost": 1, "rating_speed": 3 },
            { "name": "Gaussing",                "code": "GAUSS",        "rating_yield": 2, "rating_cost": 1, "rating_speed": 3 },
            { "name": "Kazen Winnowing",         "code": "KAZEN",        "rating_yield": 1, "rating_cost": 2, "rating_speed": 2 },
            { "name": "Pyrometric Chromalysis",  "code": "PYRO",         "rating_yield": 3, "rating_cost": 2, "rating_speed": 1 },
            { "name": "Thermonatic Deposition",  "code": "THERMO",       "rating_yield": 2, "rating_cost": 3, "rating_speed": 1 },
            { "name": "XCR Reaction",            "code": "XCR",          "rating_yield": 1, "rating_cost": 1, "rating_speed": 3 }
          ]
        }
        """;

        [Fact]
        public async Task GetRefineryMethodsAsync_FallbackPath_ReturnsNonEmptyList()
        {
            // When UEX is unreachable the service is expected to load hard-coded fallback methods
            // via LoadFallbackRefineryData(). We force the failure with a throwing HttpClient.
            var factory = StubHttpClientFactory.AlwaysThrow();
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            var methods = await sut.GetRefineryMethodsAsync();

            methods.Should().NotBeNull();
            methods.Should().NotBeEmpty("fallback data must populate at least one refinery method");

            // The fallback ships the three canonical methods (see RefineryService.LoadFallbackRefineryData).
            methods.Select(m => m.Name).Should().Contain(new[]
            {
                "Dinyx Solvents",
                "Cormack Method",
                "XCR Reaction"
            });
        }

        [Fact]
        public async Task GetRefineryMethodsAsync_FallbackPath_EveryMethodHasPositiveYieldBonus()
        {
            // Fallback path locks the pre-4.7 yield-bonus multipliers.
            var factory = StubHttpClientFactory.AlwaysThrow();
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            var methods = await sut.GetRefineryMethodsAsync();

            methods.Should().OnlyContain(m => m.YieldBonus > 0,
                "every refinery method — in either path — must advertise a positive yield multiplier");
            methods.Should().OnlyContain(m => m.CostPercent > 0,
                "every refinery method must advertise a positive cost percent");
        }

        [Fact]
        public async Task GetRefineryMethodsAsync_UexPath_ReturnsAllNineMethods()
        {
            // With the UEX endpoint reachable, all nine in-game methods must come through with
            // a positive yield multiplier. This locks the rating→percent translation in the
            // parse loop (yield 3→70, 2→50, 1→30; cost 3→15, 2→10, 1→7).
            var factory = StubHttpClientFactory.FromResponse(UexNineMethodResponse);
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            var methods = await sut.GetRefineryMethodsAsync();

            methods.Should().HaveCount(9);
            var names = methods.Select(m => m.Name).ToList();
            names.Should().Contain(new[]
            {
                "Cormack",
                "Dinyx Solventation",
                "Electrostarolysis",
                "Ferron Exchange",
                "Gaussing",
                "Kazen Winnowing",
                "Pyrometric Chromalysis",
                "Thermonatic Deposition",
                "XCR Reaction"
            });

            foreach (var method in methods)
            {
                method.YieldBonus.Should().BeGreaterThan(0, $"{method.Name} must yield a positive multiplier");
                method.CostPercent.Should().BeGreaterThan(0, $"{method.Name} must have a positive cost percent");
            }
        }

        [Theory]
        [InlineData(3, 70)]  // yield rating 3 → 70% yield bonus
        [InlineData(2, 50)]  // yield rating 2 → 50% yield bonus
        [InlineData(1, 30)]  // yield rating 1 → 30% yield bonus
        public async Task GetRefineryMethodsAsync_UexPath_YieldRatingMapsToExpectedBonus(int rating, double expectedBonus)
        {
            string body = $$"""
            {
              "data": [
                { "name": "TestMethod", "code": "TEST", "rating_yield": {{rating}}, "rating_cost": 2, "rating_speed": 2 }
              ]
            }
            """;
            var factory = StubHttpClientFactory.FromResponse(body);
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            var methods = await sut.GetRefineryMethodsAsync();

            methods.Should().ContainSingle();
            methods[0].YieldBonus.Should().Be(expectedBonus);
        }

        [Theory]
        [InlineData(3, 15)] // cost rating 3 → 15% cost
        [InlineData(2, 10)] // cost rating 2 → 10% cost
        [InlineData(1, 7)]  // cost rating 1 → 7%  cost
        public async Task GetRefineryMethodsAsync_UexPath_CostRatingMapsToExpectedPercent(int rating, double expectedCost)
        {
            string body = $$"""
            {
              "data": [
                { "name": "TestMethod", "code": "TEST", "rating_yield": 2, "rating_cost": {{rating}}, "rating_speed": 2 }
              ]
            }
            """;
            var factory = StubHttpClientFactory.FromResponse(body);
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            var methods = await sut.GetRefineryMethodsAsync();

            methods.Should().ContainSingle();
            methods[0].CostPercent.Should().Be(expectedCost);
        }

        [Fact]
        public async Task GetRefineryMethodsAsync_CachesResultAfterFirstCall()
        {
            // Guard rail: RefineryService caches _refineryMethods after the first populate.
            // A second call to the same instance must not re-hit HTTP.
            int callCount = 0;
            var handler = new CountingHandler(UexNineMethodResponse, () => callCount++);
            var factory = new CountingFactory(handler);
            var sut = new RefineryService(factory, NullLogger<RefineryService>.Instance);

            _ = await sut.GetRefineryMethodsAsync();
            _ = await sut.GetRefineryMethodsAsync();

            callCount.Should().Be(1, "second call must be served from the in-memory cache");
        }

        private sealed class CountingHandler : System.Net.Http.HttpMessageHandler
        {
            private readonly string _body;
            private readonly System.Action _onCall;
            public CountingHandler(string body, System.Action onCall) { _body = body; _onCall = onCall; }
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                _onCall();
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent(_body)
                });
            }
        }

        private sealed class CountingFactory : System.Net.Http.IHttpClientFactory
        {
            private readonly System.Net.Http.HttpMessageHandler _handler;
            public CountingFactory(System.Net.Http.HttpMessageHandler handler) { _handler = handler; }
            public System.Net.Http.HttpClient CreateClient(string name)
                => new System.Net.Http.HttpClient(_handler, disposeHandler: false) { BaseAddress = new System.Uri("https://api.uexcorp.uk/") };
        }
    }
}
