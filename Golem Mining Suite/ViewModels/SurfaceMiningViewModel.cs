using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Added
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Messages; // Added
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class SurfaceMiningViewModel : ObservableObject
    {
        private readonly IMiningDataService _miningDataService;
        private readonly IWindowService _windowService;

        [ObservableProperty]
        private string _versionText = "";

        [ObservableProperty]
        private ObservableCollection<MineralData> _featuredMinerals = new();

        [ObservableProperty]
        private string _searchText = "Search mineral...";

        [ObservableProperty]
        private bool _isSearchActive;

        [ObservableProperty]
        private ObservableCollection<string> _suggestions = new();

        [ObservableProperty]
        private bool _showSuggestions;

        private List<MineralData> _allMiningData = new();

        // Constructor injection
        public SurfaceMiningViewModel(IMiningDataService miningDataService, IWindowService windowService)
        {
            _miningDataService = miningDataService;
            _windowService = windowService;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";

            LoadData();
            Suggestions = new ObservableCollection<string>();
        }

        private void LoadData()
        {
            _allMiningData = _miningDataService.GetFeaturedSurfaceMinerals();
            FeaturedMinerals = new ObservableCollection<MineralData>(_allMiningData);
            // DEBUG: Verify count and names
            // System.Console.WriteLine($"[DEBUG] Loaded {_allMiningData.Count} minerals in SurfaceMiningViewModel.");
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(destination));
        }

        [RelayCommand]
        private void OpenPrices()
        {
            _windowService.ShowPricesWindow();
        }

        [RelayCommand]
        private void OpenCalculator()
        {
            _windowService.ShowCalculatorWindow();
        }
        
        [RelayCommand]
        private void OpenUexLink()
        {
            // MainViewModel handles logic or we duplicate it?
            // Since we removed MainViewModel dependency, we can put logic here or send message.
            // We can add OpenUexLink to IWindowService? Or just implement here.
             try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://uexcorp.space",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Could not open UEX Corp website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenMineralLocation(MineralData mineral)
        {
            if (mineral != null)
            {
                _windowService.ShowLocationWindow(mineral.MineralName, true, false, false);
            }
        }

        [RelayCommand]
        private void OpenDepositLocation(string depositName)
        {
             if(!string.IsNullOrEmpty(depositName))
             {
                 _windowService.ShowLocationWindow(depositName, false, false, false);
             }
        }

        // Search Logic
        partial void OnSearchTextChanged(string value)
        {
            if (value == "Search mineral..." || string.IsNullOrWhiteSpace(value))
            {
                ShowSuggestions = false;
                return;
            }

            var matchingMinerals = _allMiningData
                .Where(m => m.MineralName.ToLower().Contains(value.ToLower()))
                .Select(m => m.MineralName)
                .ToList();

            Suggestions.Clear();
            foreach(var m in matchingMinerals) Suggestions.Add(m);

            ShowSuggestions = Suggestions.Count > 0;
        }

        [RelayCommand]
        private void SearchGotFocus()
        {
            if (SearchText == "Search mineral...")
            {
                SearchText = "";
            }
        }

        [RelayCommand]
        private void SearchLostFocus()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = "Search mineral...";
                ShowSuggestions = false;
            }
            // Delay to allow selection? Handled by interaction trigger usually?
            // Or use a timer/delay. MvvmToolkit doesn't have built-in delay for commands on lost focus easily
            // But we can just rely on selection command firing before lost focus if designed right.
        }

        [ObservableProperty]
        private string? _selectedSuggestion; // Nullable

        partial void OnSelectedSuggestionChanged(string? value)
        {
            if (value != null)
            {
                SelectSuggestion(value);
                SelectedSuggestion = null;
            }
        }

        [RelayCommand]
        private void SelectSuggestion(string suggestion)
        {
            if(!string.IsNullOrEmpty(suggestion))
            {
                var found = _allMiningData.FirstOrDefault(m => m.MineralName == suggestion);
                if(found != null)
                {
                    OpenMineralLocation(found);
                }
                ShowSuggestions = false;
                SearchText = "Search mineral..."; // Reset
            }
        }
    }
}
