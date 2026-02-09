namespace Golem_Mining_Suite
{
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public required string CurrentVersion { get; set; }
        public required string LatestVersion { get; set; }
        public string? ReleaseUrl { get; set; }
        public required string DownloadUrl { get; set; }
        public required string ReleaseNotes { get; set; }
        public string? ReleaseName { get; set; }
    }
}
