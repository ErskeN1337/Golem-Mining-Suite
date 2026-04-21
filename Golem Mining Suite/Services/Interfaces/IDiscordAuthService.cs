using System;
using System.Threading;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Immutable snapshot of the locally-signed-in Discord user. Mirrors the subset of
    /// <c>GET https://discord.com/api/users/@me</c> we actually need plus the UTC timestamp
    /// recorded when the PKCE flow completed (purely informational — token lifetime is
    /// tracked separately inside the service).
    /// </summary>
    public sealed record DiscordAccount(
        string Id,
        string Username,
        string? GlobalName,
        string? AvatarHash,
        DateTime SignedInAtUtc);

    /// <summary>
    /// Wave 8A: browser-based Discord OAuth2 PKCE sign-in. Tokens and profile are
    /// persisted under <c>%APPDATA%\Golem Mining Suite\discord-account.json</c> so a
    /// previously-signed-in user is restored on the next launch.
    /// </summary>
    /// <remarks>
    /// Intentionally REST-only — no gateway, no presence, no bot scopes. The only
    /// requested scope is <c>identify</c>, which is enough to populate the Settings
    /// "Star Citizen Handle" field with the user's Discord username.
    /// </remarks>
    public interface IDiscordAuthService
    {
        /// <summary>
        /// Current signed-in account, or null if signed out / never signed in.
        /// Snapshot value — treat as immutable.
        /// </summary>
        DiscordAccount? CurrentAccount { get; }

        /// <summary>True when a profile + usable token pair is loaded.</summary>
        bool IsSignedIn { get; }

        /// <summary>
        /// Fired whenever <see cref="CurrentAccount"/> transitions (sign-in, sign-out,
        /// refresh-failure-induced logout). UI code observes this to refresh bindings.
        /// </summary>
        event EventHandler? SignInChanged;

        /// <summary>Open browser, run PKCE flow, store tokens. Throws on user cancel / network failure.</summary>
        Task<DiscordAccount> SignInAsync(CancellationToken ct = default);

        /// <summary>Wipe stored tokens + profile.</summary>
        Task SignOutAsync();

        /// <summary>Load any persisted account at startup. Refreshes the access token if expired.</summary>
        Task LoadAsync(CancellationToken ct = default);

        /// <summary>Returns a valid bearer token, refreshing if needed. Null if not signed in.</summary>
        Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
    }
}
