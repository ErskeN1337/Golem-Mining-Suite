using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Golem_Mining_Suite.Services.Configuration
{
    /// <summary>
    /// Resolves application secrets (Supabase URL/Key, UEX API key) from a layered set
    /// of sources, in priority order:
    ///   1. Environment variables (GOLEM_SUPABASE_URL, GOLEM_SUPABASE_KEY, GOLEM_UEX_API_KEY)
    ///   2. %APPDATA%\Golem Mining Suite\appsettings.json (user-scoped override)
    ///   3. appsettings.json shipped next to the executable (may contain placeholders)
    /// First non-empty value wins per key. All I/O failures are swallowed so the app
    /// continues to start even when no secrets are configured.
    /// </summary>
    public sealed class ResolvedSecrets
    {
        public string SupabaseUrl { get; init; } = "";
        public string SupabaseKey { get; init; } = "";
        public string UexApiKey { get; init; } = "";

        public bool IsSupabaseConfigured =>
            !string.IsNullOrWhiteSpace(SupabaseUrl) && !string.IsNullOrWhiteSpace(SupabaseKey);

        public bool IsUexConfigured => !string.IsNullOrWhiteSpace(UexApiKey);
    }

    public static class SecretResolver
    {
        private const string EnvSupabaseUrl = "GOLEM_SUPABASE_URL";
        private const string EnvSupabaseKey = "GOLEM_SUPABASE_KEY";
        private const string EnvUexApiKey = "GOLEM_UEX_API_KEY";

        /// <summary>
        /// Returns the absolute path to the user-scoped override file
        /// (%APPDATA%\Golem Mining Suite\appsettings.json). Ensures the parent
        /// directory exists but does not create the file itself.
        /// </summary>
        public static string GetUserOverridePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "Golem Mining Suite");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // Non-fatal — we just won't be able to read an override.
            }
            return Path.Combine(dir, "appsettings.json");
        }

        /// <summary>
        /// Resolves secrets from env vars, the user override file, and the shipped
        /// appsettings.json (in that priority order).
        /// </summary>
        public static ResolvedSecrets Resolve()
        {
            // Source 1: Environment variables
            var envUrl = Environment.GetEnvironmentVariable(EnvSupabaseUrl) ?? "";
            var envKey = Environment.GetEnvironmentVariable(EnvSupabaseKey) ?? "";
            var envUex = Environment.GetEnvironmentVariable(EnvUexApiKey) ?? "";

            // Source 2: %APPDATA%\Golem Mining Suite\appsettings.json
            var userSettings = TryLoadJsonSecrets(GetUserOverridePath());

            // Source 3: shipped appsettings.json
            var shippedSettings = TryLoadJsonSecrets(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"));

            return new ResolvedSecrets
            {
                SupabaseUrl = FirstNonEmpty(envUrl, userSettings.GetValueOrDefault("Supabase.Url"), shippedSettings.GetValueOrDefault("Supabase.Url")),
                SupabaseKey = FirstNonEmpty(envKey, userSettings.GetValueOrDefault("Supabase.Key"), shippedSettings.GetValueOrDefault("Supabase.Key")),
                UexApiKey = FirstNonEmpty(envUex, userSettings.GetValueOrDefault("UEX.ApiKey"), shippedSettings.GetValueOrDefault("UEX.ApiKey"))
            };
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v!;
            }
            return "";
        }

        private static Dictionary<string, string> TryLoadJsonSecrets(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return result;

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return result;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Supabase", out var supabase))
                {
                    if (supabase.TryGetProperty("Url", out var url) && url.ValueKind == JsonValueKind.String)
                        result["Supabase.Url"] = url.GetString() ?? "";
                    if (supabase.TryGetProperty("Key", out var key) && key.ValueKind == JsonValueKind.String)
                        result["Supabase.Key"] = key.GetString() ?? "";
                }

                if (root.TryGetProperty("UEX", out var uex))
                {
                    if (uex.TryGetProperty("ApiKey", out var apiKey) && apiKey.ValueKind == JsonValueKind.String)
                        result["UEX.ApiKey"] = apiKey.GetString() ?? "";
                }
            }
            catch
            {
                // Bad JSON / IO failure — treat as absent.
            }
            return result;
        }
    }
}
