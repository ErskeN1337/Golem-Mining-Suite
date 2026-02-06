using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Golem_Mining_Suite
{
    public class UpdateChecker
    {
        private static readonly HttpClient httpClient = new HttpClient();
		private const string GITHUB_API_URL = "https://api.github.com/repos/ErskeN1337/Golem-Mining-Suite/releases/latest";

		// Add User-Agent header (GitHub requires this)
		static UpdateChecker()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Golem-Mining-Suite");
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                // Get current version from assembly
                var currentVersion = GetCurrentVersion();
                
                // Fetch latest release from GitHub
                var response = await httpClient.GetAsync(GITHUB_API_URL);
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var release = JsonSerializer.Deserialize<GitHubRelease>(json);

                if (release == null || string.IsNullOrEmpty(release.tag_name))
                    return null;

                // Parse version from tag (remove 'v' prefix if present)
                var latestVersionString = release.tag_name.TrimStart('v');
                var latestVersion = new Version(latestVersionString);

                // Compare versions
                if (latestVersion > currentVersion)
                {
                    return new UpdateInfo
                    {
                        IsUpdateAvailable = true,
                        CurrentVersion = currentVersion.ToString(),
                        LatestVersion = latestVersion.ToString(),
                        ReleaseUrl = release.html_url,
                        DownloadUrl = release.assets?.Length > 0 ? release.assets[0].browser_download_url : release.html_url,
                        ReleaseNotes = release.body,
                        ReleaseName = release.name
                    };
                }

                return new UpdateInfo
                {
                    IsUpdateAvailable = false,
                    CurrentVersion = currentVersion.ToString(),
                    LatestVersion = latestVersion.ToString()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
                return null;
            }
        }

        private static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }
    }

    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public string ReleaseName { get; set; }
    }

    // GitHub API Response Model
    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string html_url { get; set; }
        public GitHubAsset[] assets { get; set; }
    }

    public class GitHubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }
}
