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

        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<TerminalInfo> _terminals = new();

        [ObservableProperty]
        private TerminalInfo? _selectedTerminal;



        public System.ComponentModel.ICollectionView FilteredTerminals { get; private set; }

        private readonly Services.Interfaces.IPriceService _priceService;

        public LiveDataViewModel(LiveDataCoordinator coordinator, Services.Interfaces.IPriceService priceService)
        {
            _coordinator = coordinator;
            _priceService = priceService;
            
            // Initialize filterable collection view
            FilteredTerminals = System.Windows.Data.CollectionViewSource.GetDefaultView(_terminals);
            FilteredTerminals.Filter = FilterTerminals;

            _coordinator.TerminalDataCaptured += OnTerminalDataCaptured;
            _coordinator.ErrorOccurred += OnErrorOccurred;

            // Check game status periodically
            System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) => UpdateGameStatus();
            timer.Start();

            // Load terminals immediately
            _ = LoadTerminalsAsync();
        }

        private async Task LoadTerminalsAsync()
        {
            if (Terminals.Count > 0) return;

            try
            {
                var terminals = await _priceService.GetTerminalsAsync();
                
                // Marshal to UI thread if needed (though ObservableCollection usually handles it if created on UI thread)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var t in terminals) Terminals.Add(t);
                    
                    if (Terminals.Count > 0 && SelectedTerminal == null)
                    {
                        // Don't auto-select to avoid overwriting "Unknown" if user hasn't chosen?
                        // Actually, leaving it null is fine, user sees placeholder or empty.
                        // Let's select the first one if we want a default, or matches manual.
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading terminals: {ex.Message}");
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredTerminals))]
        [NotifyPropertyChangedFor(nameof(IsSearchListVisible))]
        private string _searchText = "";

        public bool IsSearchListVisible => !string.IsNullOrEmpty(SearchText);

        partial void OnSearchTextChanged(string value)
        {
             FilteredTerminals.Refresh();
        }

        private bool FilterTerminals(object item)
        {
            if (string.IsNullOrEmpty(SearchText)) return true;
            if (item is TerminalInfo terminal)
            {
                return terminal.Name.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase) || 
                       terminal.StarSystem.StartsWith(SearchText, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        partial void OnSelectedTerminalChanged(TerminalInfo? value)
        {
            if (value != null)
            {
                _coordinator.SetManualLocation(value.Name, value.StarSystem);
                
                // Sync search text with selection (if not already matching to avoid loops while typing)
                if (!string.Equals(SearchText, value.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    SearchText = value.DisplayName;
                }
            }
            else
            {
                _coordinator.SetManualLocation("", "");
            }
        }

        [RelayCommand]
        private async Task ToggleLiveData()
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
                // Load terminals if empty
                if (Terminals.Count == 0)
                {
                    StatusText = "Loading terminals...";
                    var terminals = await _priceService.GetTerminalsAsync();
                    foreach (var t in terminals) Terminals.Add(t);
                    
                    // Try to auto-select if we have a saved preference or just pick one
                    if (Terminals.Count > 0) SelectedTerminal = Terminals[0];
                }

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
            // If manual location is set, update the display data too for consistency
            if (SelectedTerminal != null)
            {
                data.TerminalName = SelectedTerminal.Name;
                data.StarSystem = SelectedTerminal.StarSystem;
            }

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
