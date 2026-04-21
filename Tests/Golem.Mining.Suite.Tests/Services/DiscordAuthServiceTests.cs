// Wave 8A tests for DiscordAuthService.
//
// Coverage scope (intentional gaps documented here so future sessions don't
// accidentally "fix" them without understanding the trade-off):
//   * PKCE verifier generation — length + character set.
//   * Code challenge derivation — pinned against RFC 7636 Appendix B example.
//   * State token generation — non-empty, varies between calls.
//   * Token persistence round-trip — fields match after save/load.
//   * Refresh-when-expired — service triggers a refresh call via stubbed HTTP
//     when the persisted token is past its ExpiresAtUtc.
//
// NOT covered (would require a real browser / loopback socket):
//   * SignInAsync end-to-end — opens a real browser window and binds HttpListener
//     on 127.0.0.1:51547. We exercise the token + profile + persistence layers
//     indirectly via the refresh-expired path which uses the same internals.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Golem.Mining.Suite.Tests.Helpers;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Golem.Mining.Suite.Tests.Services
{
    public class DiscordAuthServiceTests : IDisposable
    {
        private readonly string _tempPath;

        public DiscordAuthServiceTests()
        {
            _tempPath = Path.Combine(
                Path.GetTempPath(),
                $"golem-discord-account-{Guid.NewGuid():N}.json");
        }

        public void Dispose()
        {
            try { if (File.Exists(_tempPath)) File.Delete(_tempPath); }
            catch { /* cleanup-best-effort */ }
        }

        // ---------------------------------------------------------------------------
        // PKCE verifier
        // ---------------------------------------------------------------------------

        [Fact]
        public void GenerateCodeVerifier_Is43To128UrlSafeChars()
        {
            for (int i = 0; i < 20; i++)
            {
                string verifier = DiscordAuthService.GenerateCodeVerifier();

                verifier.Length.Should().BeInRange(43, 128,
                    "RFC 7636 §4.1 requires a 43–128 char verifier");
                verifier.Should().MatchRegex(@"^[A-Za-z0-9_\-]+$",
                    "verifier must be url-safe base64 with no padding (RFC 7636 §4.1)");
            }
        }

        [Fact]
        public void GenerateCodeVerifier_IsNonDeterministic()
        {
            // 20 draws from a ~512-bit space should never collide; if this ever fires
            // the RNG is broken or GenerateCodeVerifier has been reverted to a constant.
            var set = new HashSet<string>();
            for (int i = 0; i < 20; i++)
            {
                set.Add(DiscordAuthService.GenerateCodeVerifier()).Should().BeTrue();
            }
        }

        // ---------------------------------------------------------------------------
        // Code challenge derivation (pinned against RFC 7636 Appendix B)
        // ---------------------------------------------------------------------------

        [Fact]
        public void DeriveCodeChallenge_MatchesRfc7636AppendixBExample()
        {
            // RFC 7636 Appendix B:
            //   verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
            //   challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"
            const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
            const string expected = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

            string challenge = DiscordAuthService.DeriveCodeChallenge(verifier);

            challenge.Should().Be(expected);
        }

        // ---------------------------------------------------------------------------
        // State token
        // ---------------------------------------------------------------------------

        [Fact]
        public void GenerateStateToken_IsNonEmptyAndVariesBetweenCalls()
        {
            string a = DiscordAuthService.GenerateStateToken();
            string b = DiscordAuthService.GenerateStateToken();

            a.Should().NotBeNullOrWhiteSpace();
            b.Should().NotBeNullOrWhiteSpace();
            a.Should().NotBe(b);
            a.Should().MatchRegex(@"^[A-Za-z0-9_\-]+$");
        }

        // ---------------------------------------------------------------------------
        // Persistence round-trip
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task Persistence_RoundTripsAccountAndTokens()
        {
            // Write a state file directly, then load it through the service — this is
            // what LoadAsync does on startup when the token is still fresh.
            var persisted = new DiscordAuthService.PersistedState
            {
                Account = new DiscordAccount(
                    Id: "123456789",
                    Username: "testuser",
                    GlobalName: "Test User",
                    AvatarHash: "abc123",
                    SignedInAtUtc: new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
                AccessToken = "access_abc",
                RefreshToken = "refresh_xyz",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1), // still valid — no refresh needed
            };

            await File.WriteAllTextAsync(_tempPath,
                JsonSerializer.Serialize(persisted));

            var factory = StubHttpClientFactory.FromResponse("{}"); // never called on this path
            using var sut = new DiscordAuthService(
                NullLogger<DiscordAuthService>.Instance, factory, "test-client-id", _tempPath);

            await sut.LoadAsync();

            sut.IsSignedIn.Should().BeTrue();
            sut.CurrentAccount.Should().NotBeNull();
            sut.CurrentAccount!.Id.Should().Be("123456789");
            sut.CurrentAccount.Username.Should().Be("testuser");
            sut.CurrentAccount.GlobalName.Should().Be("Test User");
            sut.CurrentAccount.AvatarHash.Should().Be("abc123");

            string? token = await sut.GetAccessTokenAsync();
            token.Should().Be("access_abc");
        }

        [Fact]
        public async Task Persistence_IsSingleLineJson()
        {
            // Locks the on-disk format — the spec explicitly says "Don't pretty-print".
            // We exercise it via the refresh path which is the only test-reachable
            // code path that writes the file.
            var persisted = new DiscordAuthService.PersistedState
            {
                Account = new DiscordAccount("1", "u", null, null, DateTime.UtcNow),
                AccessToken = "a",
                RefreshToken = "r",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // expired → triggers refresh on Load
            };
            await File.WriteAllTextAsync(_tempPath, JsonSerializer.Serialize(persisted));

            var factory = new StubHttpClientFactory(new CannedResponseHandler(new Dictionary<string, string>
            {
                ["oauth2/token"] = "{\"access_token\":\"new_access\",\"refresh_token\":\"new_refresh\",\"expires_in\":604800,\"token_type\":\"Bearer\",\"scope\":\"identify\"}",
            }));

            using var sut = new DiscordAuthService(
                NullLogger<DiscordAuthService>.Instance, factory, "test-client-id", _tempPath);

            await sut.LoadAsync();

            File.Exists(_tempPath).Should().BeTrue();
            string json = await File.ReadAllTextAsync(_tempPath);
            json.Should().NotContain("\n", "persisted JSON must be single-line per spec");
            json.Should().NotContain("  ", "persisted JSON must not be indented");
        }

        // ---------------------------------------------------------------------------
        // Refresh-when-expired
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task LoadAsync_WithExpiredToken_TriggersRefreshCall()
        {
            var persisted = new DiscordAuthService.PersistedState
            {
                Account = new DiscordAccount("42", "oldname", null, null, DateTime.UtcNow.AddDays(-7)),
                AccessToken = "old_access",
                RefreshToken = "old_refresh",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // in the past → refresh required
            };
            await File.WriteAllTextAsync(_tempPath, JsonSerializer.Serialize(persisted));

            var handler = new CannedResponseHandler(new Dictionary<string, string>
            {
                ["oauth2/token"] = "{\"access_token\":\"refreshed_access\",\"refresh_token\":\"refreshed_refresh\",\"expires_in\":604800,\"token_type\":\"Bearer\",\"scope\":\"identify\"}",
            });
            var factory = new StubHttpClientFactory(handler);

            using var sut = new DiscordAuthService(
                NullLogger<DiscordAuthService>.Instance, factory, "client-42", _tempPath);

            await sut.LoadAsync();

            handler.CallCount.Should().BeGreaterThan(0, "expired token must trigger a refresh call");
            handler.LastRequest.Should().NotBeNull();
            handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
            handler.LastRequestBody.Should().Contain("grant_type=refresh_token");
            handler.LastRequestBody.Should().Contain("refresh_token=old_refresh");

            // Service should now expose the refreshed token.
            sut.IsSignedIn.Should().BeTrue();
            string? token = await sut.GetAccessTokenAsync();
            token.Should().Be("refreshed_access");
        }

        [Fact]
        public async Task LoadAsync_WhenRefreshFails_TransitionsToSignedOutAndWipesFile()
        {
            var persisted = new DiscordAuthService.PersistedState
            {
                Account = new DiscordAccount("42", "oldname", null, null, DateTime.UtcNow.AddDays(-7)),
                AccessToken = "old_access",
                RefreshToken = "dead_refresh",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
            };
            await File.WriteAllTextAsync(_tempPath, JsonSerializer.Serialize(persisted));

            // Handler returns 400 Bad Request — Discord's response when the refresh token
            // has been revoked. The service should clear state rather than keeping stale data.
            var factory = new StubHttpClientFactory(new StatusCodeHandler(HttpStatusCode.BadRequest));

            using var sut = new DiscordAuthService(
                NullLogger<DiscordAuthService>.Instance, factory, "client-42", _tempPath);

            int signInChangedCount = 0;
            sut.SignInChanged += (_, _) => signInChangedCount++;

            await sut.LoadAsync();

            sut.IsSignedIn.Should().BeFalse();
            sut.CurrentAccount.Should().BeNull();
            File.Exists(_tempPath).Should().BeFalse(
                "sign-out must delete the account file entirely, not just blank the tokens");
            signInChangedCount.Should().Be(1, "exactly one transition: signed-in → signed-out");
        }

        [Fact]
        public async Task SignOutAsync_DeletesPersistenceFileEntirely()
        {
            var persisted = new DiscordAuthService.PersistedState
            {
                Account = new DiscordAccount("1", "u", null, null, DateTime.UtcNow),
                AccessToken = "a",
                RefreshToken = "r",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            };
            await File.WriteAllTextAsync(_tempPath, JsonSerializer.Serialize(persisted));

            var factory = StubHttpClientFactory.FromResponse("{}");
            using var sut = new DiscordAuthService(
                NullLogger<DiscordAuthService>.Instance, factory, "client", _tempPath);

            await sut.LoadAsync();
            sut.IsSignedIn.Should().BeTrue();

            await sut.SignOutAsync();

            sut.IsSignedIn.Should().BeFalse();
            sut.CurrentAccount.Should().BeNull();
            File.Exists(_tempPath).Should().BeFalse();
        }

        // ---------------------------------------------------------------------------
        // Test doubles
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Matches requests by relative URI path suffix and returns the canned body as 200 OK.
        /// Records every request so tests can assert method + body contents.
        /// </summary>
        private sealed class CannedResponseHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, string> _pathToBody;

            public int CallCount { get; private set; }
            public HttpRequestMessage? LastRequest { get; private set; }
            public string? LastRequestBody { get; private set; }

            public CannedResponseHandler(Dictionary<string, string> pathToBody)
            {
                _pathToBody = pathToBody;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                LastRequest = request;
                LastRequestBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                string path = request.RequestUri?.AbsolutePath ?? "";
                foreach (var kvp in _pathToBody)
                {
                    if (path.EndsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(kvp.Value, Encoding.UTF8, "application/json"),
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private sealed class StatusCodeHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _code;
            public StatusCodeHandler(HttpStatusCode code) { _code = code; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(_code)
                {
                    Content = new StringContent("{\"error\":\"invalid_grant\"}"),
                });
        }
    }
}
