using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Golem_Mining_Suite.Services;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class PricesViewModel : ObservableObject
    {
        private readonly IPriceService _priceService;
        private List<PriceData> _allPrices;

        [ObservableProperty]
        private ObservableCollection<PriceData> _prices;

        [ObservableProperty]
        private ObservableCollection<string> _minerals;

        [ObservableProperty]
        private string _selectedSystem = "All";

        [ObservableProperty]
        private string _selectedMineral = "All Minerals";

        [ObservableProperty]
        private string _statusText = "Loading...";

        [ObservableProperty]
        private bool _isLiveConnected;

        public PricesViewModel(IPriceService priceService)
        {
            _priceService = priceService;
            Prices = new ObservableCollection<PriceData>();
            Minerals = new ObservableCollection<string>();
            
            // Subscribe to live events
            if (_priceService is PriceService ps)
            {
                 IsLiveConnected = ps.IsLiveConnected; // Init
                 
                 ps.PricesUpdated += (s, e) => App.Current.Dispatcher.Invoke(() => 
                 {
                     StatusText = "Live Data Received";
                     ApplyFilter();
                 });
                 
                 ps.LinkStatusChanged += (s, connected) => 
                 {
                     App.Current.Dispatcher.Invoke(() => IsLiveConnected = connected);
                 };
            }
            
            LoadDataCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private async Task LoadData()
        {
            StatusText = "Loading prices from UEX Corp API...";
            _allPrices = await _priceService.GetMineralPricesAsync();

            if (_allPrices.Count > 0)
            {
                StatusText = $"Loaded {_allPrices.Count} prices (waiting for live...)";
            }
            else
            {
                StatusText = "Failed to load prices or using fallback data";
            }

            PopulateMineralFilter();
            ApplyFilter();
        }

        private void PopulateMineralFilter()
        {
            if (_allPrices == null) return;
            var minerals = _allPrices.Select(p => p.MineralName).Distinct().OrderBy(m => m).ToList();
            Minerals.Clear();
            Minerals.Add("All Minerals");
            foreach (var m in minerals) Minerals.Add(m);
            SelectedMineral = "All Minerals";
        }

        partial void OnSelectedSystemChanged(string value) => ApplyFilter();
        partial void OnSelectedMineralChanged(string value) => ApplyFilter();

        [RelayCommand]
        private void SetSystemFilter(string system)
        {
            SelectedSystem = system;
        }

        private void ApplyFilter()
        {
            if (_allPrices == null) return;

            IEnumerable<PriceData> filtered = _allPrices;

            if (SelectedSystem != "All")
            {
                filtered = filtered.Where(p => p.StarSystem != null && p.StarSystem.Contains(SelectedSystem));
            }

            if (SelectedMineral == "All Minerals" || string.IsNullOrEmpty(SelectedMineral))
            {
                // Group by mineral and take the best price for each
                filtered = filtered.GroupBy(p => p.MineralName)
                                   .Select(g => g.OrderByDescending(p => p.NumericPrice).First());
            }
            else
            {
                filtered = filtered.Where(p => p.MineralName == SelectedMineral);
            }

            var sorted = filtered.OrderByDescending(p => p.NumericPrice).ToList();
            
            Prices.Clear();
            foreach (var item in sorted) Prices.Add(item);

            StatusText = $"Showing {Prices.Count} results";
        }
    }
}
