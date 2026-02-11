using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class HaulingPricesViewModel : ObservableObject
    {
        private readonly IPriceService _priceService;
        private List<PriceData> _allPrices;

        [ObservableProperty]
        private ObservableCollection<PriceData> _prices;

        [ObservableProperty]
        private ObservableCollection<string> _commodities; // Renamed from Minerals

        [ObservableProperty]
        private string _selectedSystem = "All";

        [ObservableProperty]
        private string _selectedCommodity = "All Commodities"; // Renamed

        [ObservableProperty]
        private string _statusText = "Loading...";

        [ObservableProperty]
        private bool _isLiveConnected;

        public HaulingPricesViewModel(IPriceService priceService)
        {
            _priceService = priceService;
            Prices = new ObservableCollection<PriceData>();
            Commodities = new ObservableCollection<string>();
            
            // Subscribe to live events (casting if needed, similar to PricesViewModel)
            if (_priceService is PriceService ps)
            {
                 IsLiveConnected = ps.IsLiveConnected;
                 
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
            StatusText = "Loading commodities from UEX Corp API..."; // Updated text
            _allPrices = await _priceService.GetAllCommodityPricesAsync(); // Use new method

            if (_allPrices != null && _allPrices.Count > 0)
            {
                StatusText = $"Loaded {_allPrices.Count} commodities";
            }
            else
            {
                StatusText = "Failed to load commodities or using fallback data";
            }

            PopulateCommodityFilter();
            ApplyFilter();
        }

        private void PopulateCommodityFilter()
        {
            if (_allPrices == null) return;
            // Use MineralName as the display name (it holds the commodity name now)
            var commodities = _allPrices.Select(p => p.MineralName).Distinct().OrderBy(m => m).ToList();
            Commodities.Clear();
            Commodities.Add("All Commodities");
            foreach (var c in commodities) Commodities.Add(c);
            SelectedCommodity = "All Commodities";
        }

        partial void OnSelectedSystemChanged(string value) => ApplyFilter();
        partial void OnSelectedCommodityChanged(string value) => ApplyFilter();

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

            if (SelectedCommodity == "All Commodities" || string.IsNullOrEmpty(SelectedCommodity))
            {
                // Group by commodity and take the best price for each
                filtered = filtered.GroupBy(p => p.MineralName)
                                   .Select(g => g.OrderByDescending(p => p.NumericPrice).First());
            }
            else
            {
                filtered = filtered.Where(p => p.MineralName == SelectedCommodity);
            }

            var sorted = filtered.OrderByDescending(p => p.NumericPrice).ToList();
            
            Prices.Clear();
            foreach (var item in sorted) Prices.Add(item);

            StatusText = $"Showing {Prices.Count} results";
        }
    }
}
