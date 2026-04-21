using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Wave 8A implementation of <see cref="IDiscordAuthService"/>. Runs a standard
    /// OAuth2 PKCE flow against the Discord authorization endpoint, captures the
    /// callback via a loopback <see cref="HttpListener"/> on port 51547, exchanges
    /// the code for a bearer token (no client secret — PKCE doesn't need one), fetches
    /// the <c>identify</c> profile, and persists the result to
    /// <c>%APPDATA%\Golem Mining Suite\discord-account.json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loopback URI <c>http://127.0.0.1:51547/auth/callback</c> is registered as a
    /// redirect URI on the Discord application. Discord requires an exact match, so
    /// this value is hard-coded rather than configurable.
    /// </para>
    /// <para>
    /// Concurrency model: a single <see cref="SemaphoreSlim"/> guards token/profile
    /// mutation + file persistence. <see cref="GetAccessTokenAsync"/> is the only hot
    /// path and also serialises refreshes so a rapid burst of callers only triggers one
    /// HTTP refresh instead of N.
    /// </para>
    /// </remarks>
    public sealed class DiscordAuthService : IDiscordAuthService, IDisposable
    {
        // Discord endpoints — URLs are stable; hard-coding is fine and matches how
        // SupabaseService / UEXService treat their base URLs.
        private const string AuthorizeBaseUrl = "https://discord.com/oauth2/authorize";
        private const string TokenEndpoint = "oauth2/token"; // relative to named client base
        private const string UserEndpoint = "users/@me";     // relative to named client base

        /// <summary>
        /// Loopback redirect URI registered in the Discord developer portal for this app.
        /// Must match exactly — any drift here will cause Discord to reject the code
        /// exchange with an <c>invalid_grant</c>.
        /// </summary>
        internal const string RedirectUri = "http://127.0.0.1:51547/auth/callback";

        /// <summary>Listener prefix (trailing slash is mandatory for HttpListener).</summary>
        private const string ListenerPrefix = "http://127.0.0.1:51547/auth/callback/";

        /// <summary>
        /// Max time we wait for the user to complete the browser flow before aborting
        /// the listener and throwing <see cref="TimeoutException"/>. 5 minutes is
        /// generous — users who walk away can simply click the sign-in button again.
        /// </summary>
        internal static readonly TimeSpan BrowserFlowTimeout = TimeSpan.FromMinutes(5);

        private readonly ILogger<DiscordAuthService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _clientId;
        private readonly string _persistencePath;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private PersistedState? _state;
        private bool _disposed;

        public event EventHandler? SignInChanged;

        /// <summary>Production constructor — persists under <c>%APPDATA%</c>.</summary>
        public DiscordAuthService(
            ILogger<DiscordAuthService> logger,
            IHttpClientFactory httpClientFactory,
            string clientId)
            : this(logger, httpClientFactory, clientId, DefaultPersistencePath())
        {
        }

        /// <summary>
        /// Test-visible constructor — accepts an explicit persistence path so unit tests
        /// never touch the real AppData folder.
        /// </summary>
        internal DiscordAuthService(
            ILogger<DiscordAuthService> logger,
            IHttpClientFactory httpClientFactory,
            string clientId,
            string persistencePath)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _persistencePath = persistencePath ?? throw new ArgumentNullException(nameof(persistencePath));
        }

        public DiscordAccount? CurrentAccount => _state?.Account;

        public bool IsSignedIn => _state?.Account is not null &&
                                  !string.IsNullOrEmpty(_state.AccessToken);

        // -----------------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------------

        public async Task LoadAsync(CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            bool changed;
            try
            {
                changed = LoadFromDiskUnderLock();

                if (_state is not null && _state.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(1))
                {
                    // Token expired (or about to). Try to refresh silently; if it fails,
                    // transition to signed-out rather than leaving a dead bearer around.
                    try
                    {
                        await RefreshUnderLockAsync(ct).ConfigureAwait(false);
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Discord token refresh failed during startup; signing out.");
                        await ClearStateUnderLockAsync().ConfigureAwait(false);
                        changed = true;
                    }
                }
            }
            finally
            {
                _gate.Release();
            }

            if (changed) RaiseSignInChanged();
        }

        public async Task<DiscordAccount> SignInAsync(CancellationToken ct = default)
        {
            // PKCE parameters. Verifier is 64 random bytes base64url-encoded → ~86 chars,
            // well within the 43–128 range required by RFC 7636.
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = DeriveCodeChallenge(codeVerifier);
            string state = GenerateStateToken();

            string authUrl = BuildAuthorizationUrl(codeChallenge, state);

            // Bind listener BEFORE launching the browser — otherwise Discord may redirect
            // before the listener is ready to receive and the user sees a "can't connect"
            // page. HttpListener.Prefixes require a trailing slash.
            var listener = new HttpListener();
            listener.Prefixes.Add(ListenerPrefix);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    $"Could not bind loopback listener on {RedirectUri}. Another process may be using port 51547.",
                    ex);
            }

            string code;
            try
            {
                // Launch browser — UseShellExecute=true delegates to the default handler.
                try
                {
                    Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "Failed to launch the default browser for Discord sign-in.", ex);
                }

                code = await WaitForCallbackAsync(listener, state, ct).ConfigureAwait(false);
            }
            finally
            {
                // Guarantee the listener (and port 51547) is released even on cancel / error.
                try { listener.Stop(); } catch { /* already stopped */ }
                try { listener.Close(); } catch { /* already closed */ }
            }

            // Exchange the code for tokens.
            var tokens = await ExchangeCodeAsync(code, codeVerifier, ct).ConfigureAwait(false);

            // Fetch profile with the new bearer.
            var account = await FetchProfileAsync(tokens.AccessToken, ct).ConfigureAwait(false);

            var persisted = new PersistedState
            {
                Account = account,
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresInSeconds),
            };

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _state = persisted;
                await PersistUnderLockAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            RaiseSignInChanged();
            return account;
        }

        public async Task SignOutAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            bool wasSignedIn;
            try
            {
                wasSignedIn = _state is not null;
                await ClearStateUnderLockAsync().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            if (wasSignedIn) RaiseSignInChanged();
        }

        public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
        {
            bool needsRaise = false;
            string? token;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_state is null)
                {
                    return null;
                }

                // Treat a token within the next 60s as already expired so we don't hand
                // out a bearer that dies mid-request.
                if (_state.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(1))
                {
                    return _state.AccessToken;
                }

                try
                {
                    await RefreshUnderLockAsync(ct).ConfigureAwait(false);
                    token = _state.AccessToken;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Discord token refresh failed; signing out.");
                    await ClearStateUnderLockAsync().ConfigureAwait(false);
                    needsRaise = true;
                    token = null;
                }
            }
            finally
            {
                _gate.Release();
            }

            if (needsRaise) RaiseSignInChanged();
            return token;
        }

        // -----------------------------------------------------------------------------
        // PKCE helpers (internal so unit tests can pin the algorithm)
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Generate an RFC 7636 PKCE code verifier. 64 random bytes → ~86 base64url
        /// characters, comfortably inside the 43–128 range the spec requires.
        /// </summary>
        internal static string GenerateCodeVerifier()
        {
            byte[] buffer = RandomNumberGenerator.GetBytes(64);
            return Base64UrlEncode(buffer);
        }

        /// <summary>
        /// Derive the S256 code challenge: base64url(SHA-256(verifier)).
        /// Pinned against the RFC 7636 Appendix B example in tests.
        /// </summary>
        internal static string DeriveCodeChallenge(string verifier)
        {
            ArgumentException.ThrowIfNullOrEmpty(verifier);
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        /// <summary>CSRF-guard state token. 32 random bytes → ~43 base64url chars.</summary>
        internal static string GenerateStateToken()
        {
            byte[] buffer = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncode(buffer);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            // Standard base64url: '+' → '-', '/' → '_', strip '='.
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        // -----------------------------------------------------------------------------
        // OAuth flow internals
        // -----------------------------------------------------------------------------

        private string BuildAuthorizationUrl(string codeChallenge, string state)
        {
            // HttpUtility.UrlEncode handles '+' / '=' / ':' correctly for query strings.
            var sb = new StringBuilder(AuthorizeBaseUrl);
            sb.Append("?response_type=code");
            sb.Append("&client_id=").Append(HttpUtility.UrlEncode(_clientId));
            sb.Append("&scope=").Append(HttpUtility.UrlEncode("identify"));
            sb.Append("&redirect_uri=").Append(HttpUtility.UrlEncode(RedirectUri));
            sb.Append("&code_challenge=").Append(HttpUtility.UrlEncode(codeChallenge));
            sb.Append("&code_challenge_method=S256");
            sb.Append("&state=").Append(HttpUtility.UrlEncode(state));
            return sb.ToString();
        }

        private async Task<string> WaitForCallbackAsync(
            HttpListener listener, string expectedState, CancellationToken ct)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(BrowserFlowTimeout);

            // listener.GetContextAsync() doesn't accept a cancellation token, so we race
            // it against a cancellation-completion task. When the outer token fires we
            // call listener.Stop(), which causes GetContextAsync to throw HttpListenerException
            // — that's our escape hatch.
            using var registration = timeoutCts.Token.Register(() =>
            {
                try { listener.Stop(); } catch { /* already stopped */ }
            });

            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped — usually because the token fired.
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException(
                        $"Timed out waiting for Discord callback after {BrowserFlowTimeout.TotalMinutes:F0} minutes.");
                }
                catch (ObjectDisposedException)
                {
                    ct.ThrowIfCancellationRequested();
                    throw new TimeoutException(
                        $"Timed out waiting for Discord callback after {BrowserFlowTimeout.TotalMinutes:F0} minutes.");
                }

                NameValueCollection query = HttpUtility.ParseQueryString(
                    context.Request.Url?.Query ?? string.Empty);

                string? code = query.Get("code");
                string? state = query.Get("state");
                string? error = query.Get("error");

                await WriteResponsePageAsync(context, error).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Discord returned error: {error}");
                }

                if (string.IsNullOrEmpty(code))
                {
                    _logger.LogWarning("Discord callback had no code parameter; ignoring.");
                    continue;
                }

                if (!string.Equals(state, expectedState, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Discord callback state mismatch; ignoring (possible CSRF).");
                    continue;
                }

                return code;
            }
        }

        private static async Task WriteResponsePageAsync(HttpListenerContext context, string? error)
        {
            try
            {
                string html = string.IsNullOrEmpty(error)
                    ? "<!doctype html><html><head><meta charset=\"utf-8\"><title>Golem Mining Suite</title></head>"
                      + "<body style=\"font-family:system-ui,Segoe UI,sans-serif;background:#1e1e1e;color:#eee;display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">"
                      + "<div style=\"text-align:center\"><h1 style=\"color:#5865F2\">Signed in</h1>"
                      + "<p>You can close this tab now and return to Golem Mining Suite.</p></div></body></html>"
                    : "<!doctype html><html><head><meta charset=\"utf-8\"><title>Golem Mining Suite</title></head>"
                      + "<body style=\"font-family:system-ui,Segoe UI,sans-serif;background:#1e1e1e;color:#eee;display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">"
                      + "<div style=\"text-align:center\"><h1 style=\"color:#F04747\">Sign-in failed</h1>"
                      + "<p>You can close this tab and try again from the app.</p></div></body></html>";

                byte[] bytes = Encoding.UTF8.GetBytes(html);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.OutputStream.Close();
            }
            catch
            {
                // Best-effort — if the browser has already disconnected we just move on.
            }
        }

        private async Task<TokenResponse> ExchangeCodeAsync(
            string code, string codeVerifier, CancellationToken ct)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = _clientId,
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = RedirectUri,
            };

            return await PostTokenFormAsync(form, ct).ConfigureAwait(false);
        }

        private async Task RefreshUnderLockAsync(CancellationToken ct)
        {
            if (_state is null || string.IsNullOrEmpty(_state.RefreshToken))
            {
                throw new InvalidOperationException("No refresh token available.");
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _clientId,
                ["refresh_token"] = _state.RefreshToken,
            };

            var tokens = await PostTokenFormAsync(form, ct).ConfigureAwait(false);

            _state = _state with
            {
                AccessToken = tokens.AccessToken,
                // Discord rotates refresh tokens — fall back to the existing one if the
                // response omits it (defensive; docs say it's always returned).
                RefreshToken = string.IsNullOrEmpty(tokens.RefreshToken)
                    ? _state.RefreshToken
                    : tokens.RefreshToken,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresInSeconds),
            };

            await PersistUnderLockAsync(ct).ConfigureAwait(false);
        }

        private async Task<TokenResponse> PostTokenFormAsync(
            IDictionary<string, string> form, CancellationToken ct)
        {
            using var client = _httpClientFactory.CreateClient("discord");
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Don't log body at Warning — token responses can contain tokens in the error
                // field under some OAuth providers. Status code is enough to diagnose.
                _logger.LogWarning(
                    "Discord token endpoint returned {Status}.", (int)response.StatusCode);
                throw new HttpRequestException(
                    $"Discord token endpoint returned HTTP {(int)response.StatusCode}.");
            }

            var parsed = JsonSerializer.Deserialize<TokenResponse>(body, s_jsonOptions)
                ?? throw new InvalidOperationException("Discord token response was empty.");

            if (string.IsNullOrEmpty(parsed.AccessToken))
            {
                throw new InvalidOperationException("Discord token response had no access_token.");
            }

            return parsed;
        }

        private async Task<DiscordAccount> FetchProfileAsync(string accessToken, CancellationToken ct)
        {
            using var client = _httpClientFactory.CreateClient("discord");
            using var request = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Discord profile endpoint returned HTTP {(int)response.StatusCode}.");
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var user = JsonSerializer.Deserialize<DiscordUserResponse>(body, s_jsonOptions)
                ?? throw new InvalidOperationException("Discord user response was empty.");

            if (string.IsNullOrEmpty(user.Id) || string.IsNullOrEmpty(user.Username))
            {
                throw new InvalidOperationException("Discord user response missing id or username.");
            }

            return new DiscordAccount(
                Id: user.Id,
                Username: user.Username,
                GlobalName: user.GlobalName,
                AvatarHash: user.Avatar,
                SignedInAtUtc: DateTime.UtcNow);
        }

        // -----------------------------------------------------------------------------
        // Persistence
        // -----------------------------------------------------------------------------

        private bool LoadFromDiskUnderLock()
        {
            if (!File.Exists(_persistencePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(_persistencePath);
                if (string.IsNullOrWhiteSpace(json)) return false;

                var loaded = JsonSerializer.Deserialize<PersistedState>(json, s_jsonOptions);
                if (loaded is null || loaded.Account is null) return false;

                _state = loaded;
                return true;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                _logger.LogWarning(ex,
                    "Failed to read Discord account from {Path}; treating as signed-out.",
                    _persistencePath);
                return false;
            }
        }

        private async Task PersistUnderLockAsync(CancellationToken ct)
        {
            if (_state is null)
            {
                await DeleteFileUnderLockAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                string? dir = Path.GetDirectoryName(_persistencePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Single-line JSON — keeps the file compact and matches the spec.
                string json = JsonSerializer.Serialize(_state, s_persistOptions);
                await File.WriteAllTextAsync(_persistencePath, json, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex,
                    "Failed to persist Discord account to {Path}.", _persistencePath);
            }
        }

        private async Task ClearStateUnderLockAsync()
        {
            _state = null;
            await DeleteFileUnderLockAsync().ConfigureAwait(false);
        }

        private Task DeleteFileUnderLockAsync()
        {
            try
            {
                if (File.Exists(_persistencePath))
                {
                    File.Delete(_persistencePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex,
                    "Failed to delete Discord account file at {Path}.", _persistencePath);
            }
            return Task.CompletedTask;
        }

        private void RaiseSignInChanged()
        {
            try
            {
                SignInChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignInChanged handler threw.");
            }
        }

        internal static string DefaultPersistencePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Golem Mining Suite");
            return Path.Combine(dir, "discord-account.json");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gate.Dispose();
        }

        // -----------------------------------------------------------------------------
        // Serialisation support
        // -----------------------------------------------------------------------------

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // Separate options for persistence so we can drop WriteIndented — keeps the on-disk
        // file to a single line as required by the spec.
        private static readonly JsonSerializerOptions s_persistOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Shape that actually hits disk. Kept internal + record so its layout is greppable
        /// and tests can round-trip it directly.
        /// </summary>
        internal sealed record PersistedState
        {
            public DiscordAccount? Account { get; init; }
            public string AccessToken { get; init; } = string.Empty;
            public string RefreshToken { get; init; } = string.Empty;
            public DateTime ExpiresAtUtc { get; init; }
        }

        private sealed class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresInSeconds { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; } = string.Empty;

            [JsonPropertyName("scope")]
            public string Scope { get; set; } = string.Empty;
        }

        private sealed class DiscordUserResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("username")]
            public string Username { get; set; } = string.Empty;

            [JsonPropertyName("global_name")]
            public string? GlobalName { get; set; }

            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }
        }
    }
}
