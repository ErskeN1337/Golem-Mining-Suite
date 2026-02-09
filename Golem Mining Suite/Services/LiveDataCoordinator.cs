using Golem_Mining_Suite.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Coordinates live data collection from Star Citizen terminals
    /// </summary>
    public class LiveDataCoordinator : IDisposable
    {
        private readonly GameDetectionService _gameDetection;
        private readonly OCRService _ocrService;
        private readonly TerminalParser _parser;
        private readonly SupabaseService? _supabaseService;
        private readonly string _logFilePath;
        
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitoringTask;
        private bool _isEnabled = false;
        private DateTime _lastCapture = DateTime.MinValue;
        
        private const int CAPTURE_INTERVAL_SECONDS = 30; // Rate limit: 1 capture per 30 seconds
        private const int MONITORING_INTERVAL_MS = 5000; // Check every 5 seconds

        public event EventHandler<TerminalData>? TerminalDataCaptured;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsEnabled => _isEnabled;
        public bool IsGameRunning => _gameDetection.IsStarCitizenRunning();

        public LiveDataCoordinator()
        {
            _gameDetection = new GameDetectionService();
            var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _ocrService = new OCRService(tessDataPath);
            _parser = new TerminalParser();
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "livedata_debug.log");
            
            // Clear old log on startup
            try { File.WriteAllText(_logFilePath, $"=== Live Data Debug Log - {DateTime.Now} ===\n"); } catch { }
            
            // Load Supabase configuration
            try
            {
                var config = LoadConfiguration();
                if (config != null)
                {
                    _supabaseService = new SupabaseService(config.Url, config.Key);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LiveData] Failed to load Supabase config: {ex.Message}");
            }
        }

        /// <summary>
        /// Start monitoring for terminal data
        /// </summary>
        public async Task<bool> StartAsync()
        {
            LogDebug("StartAsync called");
            
            if (_isEnabled)
            {
                LogDebug("Already enabled, returning true");
                return true;
            }

            // Initialize OCR engine
            LogDebug("Initializing OCR engine...");
            if (!_ocrService.Initialize())
            {
                LogDebug("OCR initialization failed!");
                ErrorOccurred?.Invoke(this, "Failed to initialize OCR engine. Ensure tessdata folder exists.");
                return false;
            }

            // Initialize Supabase if available
            if (_supabaseService != null)
            {
                try
                {
                    LogDebug("Initializing Supabase...");
                    var supabaseInitialized = await _supabaseService.InitializeAsync();
                    if (!supabaseInitialized)
                    {
                        LogDebug("Supabase initialization failed (non-critical)");
                    }
                    else
                    {
                        LogDebug("Supabase initialized successfully");
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Supabase initialization exception: {ex.Message}");
                }
            }
            else
            {
                LogDebug("No Supabase service configured");
            }

            LogDebug("Starting monitoring task...");
            _isEnabled = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));
            
            LogDebug("Monitoring started successfully");
            return true;
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void Stop()
        {
            if (!_isEnabled)
                return;

            _isEnabled = false;
            _cancellationTokenSource?.Cancel();
            _monitoringTask?.Wait(TimeSpan.FromSeconds(5));
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private async Task MonitoringLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if game is running
                    if (!_gameDetection.IsStarCitizenRunning())
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Check rate limiting
                    if ((DateTime.Now - _lastCapture).TotalSeconds < CAPTURE_INTERVAL_SECONDS)
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Check if possibly at terminal
                    if (!_gameDetection.IsPossiblyAtTerminal())
                    {
                        await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
                        continue;
                    }

                    // Attempt to capture terminal data
                    var terminalData = CaptureTerminalData();
                    if (terminalData != null && terminalData.IsValid())
                    {
                        _lastCapture = DateTime.Now;
                        TerminalDataCaptured?.Invoke(this, terminalData);
                        
                        // Upload to Supabase if available
                        if (_supabaseService != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                var uploaded = await _supabaseService.UploadTerminalDataAsync(terminalData);
                                if (uploaded)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[LiveData] Uploaded: {terminalData.CommodityName} at {terminalData.TerminalName}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[LiveData] Upload failed for {terminalData.CommodityName}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Monitoring error: {ex.Message}");
                }

                await Task.Delay(MONITORING_INTERVAL_MS, cancellationToken);
            }
        }

        private TerminalData? CaptureTerminalData()
        {
            try
            {
                LogDebug("Starting capture attempt...");
                
                // Get game window bounds
                var bounds = _gameDetection.GetWindowBounds();
                if (!bounds.HasValue)
                {
                    LogDebug("Failed to get window bounds");
                    return null;
                }

                LogDebug($"Window bounds: {bounds.Value.Width}x{bounds.Value.Height} at ({bounds.Value.X}, {bounds.Value.Y})");

                // Estimate terminal region
                var terminalRegion = _ocrService.EstimateTerminalRegion(
                    bounds.Value.X,
                    bounds.Value.Y,
                    bounds.Value.Width,
                    bounds.Value.Height
                );

                if (!terminalRegion.HasValue)
                {
                    LogDebug("Failed to estimate terminal region");
                    return null;
                }

                LogDebug($"Terminal region: {terminalRegion.Value.Width}x{terminalRegion.Value.Height} at ({terminalRegion.Value.X}, {terminalRegion.Value.Y})");

                // Capture and extract text
                var ocrText = _ocrService.CaptureAndExtractText(
                    terminalRegion.Value.X,
                    terminalRegion.Value.Y,
                    terminalRegion.Value.Width,
                    terminalRegion.Value.Height
                );

                if (string.IsNullOrWhiteSpace(ocrText))
                {
                    LogDebug("OCR returned empty text");
                    return null;
                }

                LogDebug($"OCR extracted {ocrText.Length} characters");
                LogDebug($"OCR Text Preview: {ocrText.Substring(0, Math.Min(200, ocrText.Length))}");

                // Parse terminal data
                var parsedData = _parser.ParseTerminalText(ocrText);
                if (parsedData == null)
                {
                    LogDebug("Parser failed to extract terminal data");
                }
                else
                {
                    LogDebug($"Successfully parsed: {parsedData.CommodityName} at {parsedData.TerminalName}");
                }
                
                return parsedData;
            }
            catch (Exception ex)
            {
                LogDebug($"Capture exception: {ex.Message}");
                return null;
            }
        }

        private void LogDebug(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            System.Diagnostics.Debug.WriteLine($"[LiveData] {message}");
            try
            {
                File.AppendAllText(_logFilePath, logMessage + "\n");
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _ocrService?.Dispose();
        }

        private SupabaseConfig? LoadConfiguration()
        {
            try
            {
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(appSettingsPath))
                {
                    System.Diagnostics.Debug.WriteLine("[LiveData] appsettings.json not found");
                    return null;
                }

                var json = File.ReadAllText(appSettingsPath);
                var doc = JsonDocument.Parse(json);
                
                if (doc.RootElement.TryGetProperty("Supabase", out var supabaseElement))
                {
                    var url = supabaseElement.GetProperty("Url").GetString();
                    var key = supabaseElement.GetProperty("Key").GetString();
                    
                    if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(key))
                    {
                        return new SupabaseConfig { Url = url, Key = key };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LiveData] Config load error: {ex.Message}");
            }
            
            return null;
        }

        private class SupabaseConfig
        {
            public string Url { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
        }
    }
}
