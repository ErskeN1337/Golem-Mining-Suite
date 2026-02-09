using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Golem_Mining_Suite.Messages;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Golem_Mining_Suite.ViewModels
{
    public class AsteroidMineralGroup
    {
        public string MineralName { get; set; }
        public string OreTypesDisplay { get; set; }
    }

    public partial class AsteroidMiningViewModel : ObservableObject
    {
        private readonly IMiningDataService _miningDataService;
        private readonly IWindowService _windowService;

        [ObservableProperty]
        private string _versionText = "";

        [ObservableProperty]
        private string _searchText = "Search mineral...";

        [ObservableProperty]
        private bool _isSearchActive;

        [ObservableProperty]
        private ObservableCollection<string> _suggestions = new();

        [ObservableProperty]
        private bool _showSuggestions;

        private List<AsteroidMineralData> _allMiningData = new();
        private List<AsteroidMineralGroup> _groupedMinerals = new();

        public AsteroidMiningViewModel(IMiningDataService miningDataService, IWindowService windowService)
        {
            _miningDataService = miningDataService;
            _windowService = windowService;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";

            Suggestions = new ObservableCollection<string>();
            LoadData();
        }

        [ObservableProperty]
        private ObservableCollection<AsteroidMineralGroup> _minerals = new();

        private void LoadData()
        {
            _allMiningData = _miningDataService.GetAsteroidMinerals();
            
            // Group by MineralName to remove duplicates and aggregate OreTypes
            _groupedMinerals = _allMiningData
                .GroupBy(m => m.MineralName)
                .Select(g => new AsteroidMineralGroup
                {
                    MineralName = g.Key,
                    OreTypesDisplay = string.Join(", ", g.Select(m => m.OreType).Distinct().OrderBy(t => t))
                })
                .OrderBy(m => m.MineralName)
                .ToList();

            Minerals = new ObservableCollection<AsteroidMineralGroup>(_groupedMinerals);
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
        private void OpenLocation(string mineralName)
        {
            if (!string.IsNullOrEmpty(mineralName))
            {
                // true for mineral search, true for asteroid mode
                _windowService.ShowLocationWindow(mineralName, true, true, false);
            }
        }

        // Search Logic
        partial void OnSearchTextChanged(string value)
        {
            if (value == "Search mineral..." || string.IsNullOrWhiteSpace(value))
            {
                ShowSuggestions = false;
                Minerals = new ObservableCollection<AsteroidMineralGroup>(_groupedMinerals);
                return;
            }

            var matchingGroups = _groupedMinerals
                .Where(m => m.MineralName.ToLower().Contains(value.ToLower()))
                .ToList();

            Suggestions.Clear();
            foreach(var m in matchingGroups) Suggestions.Add(m.MineralName);

            ShowSuggestions = Suggestions.Count > 0;
            Minerals = new ObservableCollection<AsteroidMineralGroup>(matchingGroups);
        }

        [ObservableProperty]
        private string? _selectedSuggestion;

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
            if (!string.IsNullOrEmpty(suggestion))
            {
                // Find the group
                var group = _groupedMinerals.FirstOrDefault(g => g.MineralName == suggestion);
                if (group != null)
                {
                     Minerals = new ObservableCollection<AsteroidMineralGroup> { group };
                }
                
                ShowSuggestions = false;
                // Keep the search text so user knows what is selected, or clear it? 
                // Typically if we select a suggestion, we might want to just show that one result.
                // Or OpenLocation directly?
                // The original logic opened location.
                OpenLocation(suggestion);
                
                SearchText = "Search mineral...";
                Minerals = new ObservableCollection<AsteroidMineralGroup>(_groupedMinerals); // Reset list after opening logic
            }
        }
    }
}
