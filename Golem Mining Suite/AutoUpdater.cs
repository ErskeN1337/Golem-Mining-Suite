using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Golem_Mining_Suite
{
    public class AutoUpdater
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, IProgress<int> progress)
        {
            try
            {
                // Get the download URL (first asset from the release)
                string downloadUrl = updateInfo.DownloadUrl;
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("No download URL found in the release.", 
                        "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Download the new version to temp folder
                string tempPath = Path.Combine(Path.GetTempPath(), "GolemMiningUpdate");
                Directory.CreateDirectory(tempPath);
                
                string updateFileName = "Golem Mining Suite.exe";
                string downloadedFile = Path.Combine(tempPath, updateFileName);

                // Download the file
                using (var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(downloadedFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progressPercentage = (int)((totalRead * 100) / totalBytes);
                                progress?.Report(progressPercentage);
                            }
                        }
                    }
                }

                // Create updater script
                CreateUpdaterScript(downloadedFile);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download update: {ex.Message}", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void CreateUpdaterScript(string downloadedFile)
        {
            // Get current exe path
            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
            string currentExeDir = Path.GetDirectoryName(currentExePath);
            
            // Create a batch script that will:
            // 1. Wait for current app to close
            // 2. Replace the old exe with new one
            // 3. Start the new exe
            // 4. Delete itself

            string batchScript = $@"@echo off
timeout /t 2 /nobreak > nul
echo Updating Golem Mining Suite...
move /Y ""{downloadedFile}"" ""{currentExePath}""
if errorlevel 1 (
    echo Update failed!
    pause
    exit
)
echo Update complete! Starting application...
start """" ""{currentExePath}""
del ""%~f0""
";

            string batchPath = Path.Combine(Path.GetTempPath(), "update_golem.bat");
            File.WriteAllText(batchPath, batchScript);

            // Start the batch script and exit current app
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi);
            
            // Exit the current application
            Application.Current.Shutdown();
        }

        public static async Task<bool> DownloadUpdateWithProgressAsync(UpdateInfo updateInfo, Action<int> progressCallback)
        {
            var progress = new Progress<int>(progressCallback);
            return await DownloadAndInstallUpdateAsync(updateInfo, progress);
        }
    }
}
