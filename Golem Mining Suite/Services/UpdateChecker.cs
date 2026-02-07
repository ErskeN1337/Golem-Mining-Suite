using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;

namespace Golem_Mining_Suite
{
	public static class UpdateChecker
	{
		private const string GITHUB_API_URL = "https://api.github.com/repos/ErskeN1337/Golem-Mining-Suite/releases/latest";

		public static async Task<UpdateInfo> CheckForUpdateAsync()
		{
			try
			{
				using (var client = new HttpClient())
				{
					client.DefaultRequestHeaders.Add("User-Agent", "Golem-Mining-Suite");
					client.Timeout = TimeSpan.FromSeconds(10);

					var response = await client.GetStringAsync(GITHUB_API_URL);
					var jsonDoc = JsonDocument.Parse(response);
					var root = jsonDoc.RootElement;

					string latestVersion = root.GetProperty("tag_name").GetString().Replace("v", "");
					string downloadUrl = "";
					string releaseNotes = "";

					// Try to get release notes
					if (root.TryGetProperty("body", out var bodyElement))
					{
						releaseNotes = bodyElement.GetString();
					}

					// Get the ZIP file download URL
					var assets = root.GetProperty("assets");
					foreach (var asset in assets.EnumerateArray())
					{
						string assetName = asset.GetProperty("name").GetString();
						if (assetName.EndsWith(".zip"))
						{
							downloadUrl = asset.GetProperty("browser_download_url").GetString();
							break;
						}
					}

					// Get current version
					var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
					string currentVersionString = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

					// Compare versions
					bool isNewer = IsNewerVersion(latestVersion, currentVersionString);

					return new UpdateInfo
					{
						IsUpdateAvailable = isNewer,
						LatestVersion = latestVersion,
						CurrentVersion = currentVersionString,
						DownloadUrl = downloadUrl,
						ReleaseNotes = releaseNotes
					};
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static bool IsNewerVersion(string latestVersion, string currentVersion)
		{
			try
			{
				var latest = new Version(latestVersion);
				var current = new Version(currentVersion);
				return latest > current;
			}
			catch
			{
				return false;
			}
		}
	}
}