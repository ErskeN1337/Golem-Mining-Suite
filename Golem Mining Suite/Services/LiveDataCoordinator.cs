using Golem_Mining_Suite.Models;
using System;
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
            _ocrService = new OCRService();
            _parser = new TerminalParser();
        }

        /// <summary>
        /// Start monitoring for terminal data
        /// </summary>
        public bool Start()
        {
            if (_isEnabled)
                return true;

            // Initialize OCR engine
            if (!_ocrService.Initialize())
            {
                ErrorOccurred?.Invoke(this, "Failed to initialize OCR engine. Ensure tessdata folder exists.");
                return false;
            }

            _isEnabled = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitoringLoop(_cancellationTokenSource.Token));
            
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
                // Get game window bounds
                var bounds = _gameDetection.GetWindowBounds();
                if (!bounds.HasValue)
                    return null;

                // Estimate terminal region
                var terminalRegion = _ocrService.EstimateTerminalRegion(
                    bounds.Value.X,
                    bounds.Value.Y,
                    bounds.Value.Width,
                    bounds.Value.Height
                );

                if (!terminalRegion.HasValue)
                    return null;

                // Capture and extract text
                var ocrText = _ocrService.CaptureAndExtractText(
                    terminalRegion.Value.X,
                    terminalRegion.Value.Y,
                    terminalRegion.Value.Width,
                    terminalRegion.Value.Height
                );

                if (string.IsNullOrWhiteSpace(ocrText))
                    return null;

                // Parse terminal data
                return _parser.ParseTerminalText(ocrText);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
            _ocrService?.Dispose();
        }
    }
}
