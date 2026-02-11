using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Golem_Mining_Suite.Messages;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class ROCMiningViewModel : ObservableObject
    {
        private readonly IMiningDataService _miningDataService;
        private readonly IWindowService _windowService;

        [ObservableProperty]
        private string _versionText;

        [ObservableProperty]
        private string _searchText = "Search rock type...";

        [ObservableProperty]
        private bool _isSearchActive;

        [ObservableProperty]
        private ObservableCollection<string> _suggestions;

        [ObservableProperty]
        private bool _showSuggestions;

        private List<string> _allRockTypes;

        public ROCMiningViewModel(IMiningDataService miningDataService, IWindowService windowService)
        {
            _miningDataService = miningDataService;
            _windowService = windowService;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
                VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";
            else
                VersionText = "v1.0.0";

            _allRockTypes = new List<string>();
            RockTypes = new ObservableCollection<string>();

            LoadData();
            Suggestions = new ObservableCollection<string>();
        }

        [ObservableProperty]
        private ObservableCollection<string> _rockTypes;

        private void LoadData()
        {
            _allRockTypes = _miningDataService.GetROCRockTypes();
            RockTypes = new ObservableCollection<string>(_allRockTypes);
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
        private void OpenLocation(string rockType)
        {
            if (!string.IsNullOrEmpty(rockType))
            {
                // false for isMineral (it's a rock type/deposit technically, or treated as mineral?), false for asteroid, true for roc
                // Original code: locationWindow = new LocationWindow(rockType, false, false, true);
                _windowService.ShowLocationWindow(rockType, false, false, true);
            }
        }

        // Search Logic
        partial void OnSearchTextChanged(string value)
        {
            if (value == "Search rock type..." || string.IsNullOrWhiteSpace(value))
            {
                ShowSuggestions = false;
                return;
            }

            var matchingRocks = _allRockTypes
                .Where(r => r.ToLower().Contains(value.ToLower()))
                .ToList();

            Suggestions.Clear();
            foreach(var r in matchingRocks) Suggestions.Add(r);

            ShowSuggestions = Suggestions.Count > 0;
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
                OpenLocation(suggestion);
                ShowSuggestions = false;
                SearchText = "Search rock type...";
            }
        }
    }
}
