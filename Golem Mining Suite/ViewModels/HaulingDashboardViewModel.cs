using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Messages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class HaulingDashboardViewModel : ObservableObject
    {
        private readonly ICommodityDataService _commodityDataService;
        private readonly IWindowService _windowService;
        private System.Collections.Generic.List<CommodityData> _allCommodities = new();

        [ObservableProperty]
        private string _searchText = "Search commodity...";
        
        [ObservableProperty]
        private bool _showSuggestions;

        [ObservableProperty]
        private ObservableCollection<string> _suggestions = new();

        [ObservableProperty]
        private ObservableCollection<CommodityData> _featuredCommodities = new();

        [ObservableProperty]
        private bool _isLoading;

        public HaulingDashboardViewModel(ICommodityDataService commodityDataService, IWindowService windowService)
        {
            _commodityDataService = commodityDataService;
            _windowService = windowService;

            System.Diagnostics.Debug.WriteLine("[HaulingVM] Initializing...");
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            System.Diagnostics.Debug.WriteLine("[HaulingVM] Loading data...");
            IsLoading = true;
            try
            {
                var data = await _commodityDataService.GetAllCommoditiesAsync();
                System.Diagnostics.Debug.WriteLine($"[HaulingVM] Received {data.Count} commodities.");
                _allCommodities = data;
                FeaturedCommodities = new ObservableCollection<CommodityData>(_allCommodities);
            }
            catch (System.Exception ex)
            {
                 System.Diagnostics.Debug.WriteLine($"[HaulingVM] Error loading data: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenCommodityInfo(CommodityData commodity)
        {
            if (commodity == null) return;
            // TODO: Implement a window or dialog to show trading details (buy/sell locations)
            // For now, maybe just search or placeholder?
            // _windowService.ShowCommodityWindow(commodity.Name); // If it existed
            System.Windows.MessageBox.Show($"Selected: {commodity.Name}\nAvg Buy: {commodity.AveragePriceBuy}\nAvg Sell: {commodity.AveragePriceSell}", "Commodity Info");
        }

        // Search Logic
        partial void OnSearchTextChanged(string value)
        {
             if (value == "Search commodity..." || string.IsNullOrWhiteSpace(value))
            {
                ShowSuggestions = false;
                // Reset filter
                if (!IsLoading && _allCommodities.Count > 0) 
                    FeaturedCommodities = new ObservableCollection<CommodityData>(_allCommodities);
                return;
            }

            var matching = _allCommodities
                .Where(c => c.Name.ToLower().Contains(value.ToLower()))
                .ToList();

            // Filter main list
            FeaturedCommodities = new ObservableCollection<CommodityData>(matching);

            // Update suggestions
            Suggestions.Clear();
            foreach(var c in matching.Take(5)) Suggestions.Add(c.Name);
            ShowSuggestions = Suggestions.Count > 0;
        }

        [RelayCommand]
        private void SearchGotFocus()
        {
            if (SearchText == "Search commodity...") SearchText = "";
        }

        [RelayCommand]
        private void SearchLostFocus()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = "Search commodity...";
                ShowSuggestions = false;
            }
        }

        [RelayCommand]
        private void SelectSuggestion(string suggestion)
        {
             if(!string.IsNullOrEmpty(suggestion))
            {
                var found = _allCommodities.FirstOrDefault(c => c.Name == suggestion);
                if(found != null)
                {
                    OpenCommodityInfo(found);
                }
                ShowSuggestions = false;
                SearchText = "Search commodity..."; // Reset
                FeaturedCommodities = new ObservableCollection<CommodityData>(_allCommodities);
            }
        }
    }
}
