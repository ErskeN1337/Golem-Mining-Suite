using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
                // Get the download URL - should be a ZIP file
                string downloadUrl = updateInfo.DownloadUrl;
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("No download URL found in the release.", 
                        "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Create temp directory for update
                string tempPath = Path.Combine(Path.GetTempPath(), "GolemMiningUpdate_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempPath);
                
                string downloadedFile = Path.Combine(tempPath, "update.zip");
                string extractPath = Path.Combine(tempPath, "extracted");

                // Download the ZIP file
                progress?.Report(0);
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
                                var progressPercentage = (int)((totalRead * 70) / totalBytes); // 0-70%
                                progress?.Report(progressPercentage);
                            }
                        }
                    }
                }

                progress?.Report(75); // Download complete, starting extraction

                // Extract the ZIP file
                Directory.CreateDirectory(extractPath);
                ZipFile.ExtractToDirectory(downloadedFile, extractPath);

                progress?.Report(85); // Extraction complete

                // Create updater script that will replace all files
                CreateAdvancedUpdaterScript(extractPath);

                progress?.Report(100); // Ready to update

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download update: {ex.Message}", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void CreateAdvancedUpdaterScript(string extractPath)
        {
            // Get current app directory
            string currentExePath = Process.GetCurrentProcess().MainModule.FileName;
            string currentAppDir = Path.GetDirectoryName(currentExePath);
            string exeName = Path.GetFileName(currentExePath);
            
            // Create a PowerShell script for better file handling
            string psScript = $@"
# Wait for main app to close
Start-Sleep -Seconds 2

# Backup current version (optional)
$backupDir = Join-Path $env:TEMP 'GolemMiningBackup'
if (Test-Path $backupDir) {{ Remove-Item $backupDir -Recurse -Force }}
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

Write-Host 'Creating backup...'
Copy-Item '{currentAppDir}\*' $backupDir -Recurse -Force

try {{
    Write-Host 'Installing update...'
    
    # Copy all files from update to app directory
    Get-ChildItem '{extractPath}' -Recurse | ForEach-Object {{
        $targetPath = $_.FullName.Replace('{extractPath}', '{currentAppDir}')
        
        if ($_.PSIsContainer) {{
            if (!(Test-Path $targetPath)) {{
                New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
            }}
        }} else {{
            Copy-Item $_.FullName $targetPath -Force
        }}
    }}
    
    Write-Host 'Update complete!'
    
    # Clean up temp files
    Start-Sleep -Seconds 1
    Remove-Item '{extractPath}' -Recurse -Force -ErrorAction SilentlyContinue
    
    # Start the updated app
    Start-Process '{currentExePath}'
    
}} catch {{
    Write-Host 'Update failed! Restoring backup...'
    Copy-Item '$backupDir\*' '{currentAppDir}' -Recurse -Force
    Start-Process '{currentExePath}'
}}
";

            string psScriptPath = Path.Combine(Path.GetTempPath(), "update_golem.ps1");
            File.WriteAllText(psScriptPath, psScript);

            // Create a batch file to run the PowerShell script
            string batchScript = $@"@echo off
title Golem Mining Suite Updater
echo Updating Golem Mining Suite...
powershell.exe -ExecutionPolicy Bypass -File ""{psScriptPath}""
del ""%~f0""
";

            string batchPath = Path.Combine(Path.GetTempPath(), "update_golem.bat");
            File.WriteAllText(batchPath, batchScript);

            // Start the updater and exit current app
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = batchPath,
                CreateNoWindow = false, // Show window so user can see progress
                UseShellExecute = true,
                Verb = "runas" // Request admin rights if needed
            };

            try
            {
                Process.Start(psi);
                
                // Give the script a moment to start
                System.Threading.Thread.Sleep(500);
                
                // Exit the current application
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start updater: {ex.Message}\n\n" +
                               "You may need to run as administrator.", 
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static async Task<bool> DownloadUpdateWithProgressAsync(UpdateInfo updateInfo, Action<int> progressCallback)
        {
            var progress = new Progress<int>(progressCallback);
            return await DownloadAndInstallUpdateAsync(updateInfo, progress);
        }
    }
}
