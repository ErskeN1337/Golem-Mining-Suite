using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services;
using System;

namespace Golem_Mining_Suite.ViewModels
{
    /// <summary>
    /// ViewModel for Live Data settings and status
    /// </summary>
    public partial class LiveDataViewModel : ObservableObject
    {
        private readonly LiveDataCoordinator _coordinator;

        [ObservableProperty]
        private bool _isEnabled = false;

        [ObservableProperty]
        private bool _isGameRunning = false;

        [ObservableProperty]
        private string _statusText = "Disabled";

        [ObservableProperty]
        private int _contributionCount = 0;

        [ObservableProperty]
        private string _lastUpdateText = "Never";

        [ObservableProperty]
        private string _gameStatusText = "Not Detected";

        [ObservableProperty]
        private string _gameStatusColor = "#F44336";

        public LiveDataViewModel(LiveDataCoordinator coordinator)
        {
            _coordinator = coordinator;
            _coordinator.TerminalDataCaptured += OnTerminalDataCaptured;
            _coordinator.ErrorOccurred += OnErrorOccurred;

            // Check game status periodically
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) => UpdateGameStatus();
            timer.Start();
        }

        [RelayCommand]
        private async void ToggleLiveData()
        {
            if (IsEnabled)
            {
                // Disable
                _coordinator.Stop();
                IsEnabled = false;
                StatusText = "Disabled";
            }
            else
            {
                // Enable
                StatusText = "Starting...";
                if (await _coordinator.StartAsync())
                {
                    IsEnabled = true;
                    StatusText = IsGameRunning ? "Monitoring..." : "Waiting for game...";
                }
                else
                {
                    StatusText = "Failed to start (check tessdata)";
                }
            }
        }

        private void OnTerminalDataCaptured(object? sender, TerminalData data)
        {
            ContributionCount++;
            LastUpdateText = $"{DateTime.Now:HH:mm:ss}";
            
            System.Diagnostics.Debug.WriteLine($"[LiveData] Captured: {data.CommodityName} at {data.TerminalName} - {data.PriceSell} aUEC");
        }

        private void OnErrorOccurred(object? sender, string error)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveData] Error: {error}");
            StatusText = $"Error: {error}";
        }

        private void UpdateGameStatus()
        {
            IsGameRunning = _coordinator.IsGameRunning;
            
            if (IsGameRunning)
            {
                GameStatusText = "Running";
                GameStatusColor = "#4CAF50";
            }
            else
            {
                GameStatusText = "Not Detected";
                GameStatusColor = "#F44336";
            }
        }
    }
}
