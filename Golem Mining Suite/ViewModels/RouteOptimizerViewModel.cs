using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class RouteOptimizerViewModel : ObservableObject
    {
        private readonly IPriceService _priceService;
        private readonly RouteOptimizerService _routeOptimizer;

        [ObservableProperty]
        private ObservableCollection<TradeRoute> _routes = new();

        [ObservableProperty]
        private ObservableCollection<string> _ships = new();

        [ObservableProperty]
        private string _selectedShip = "";

        [ObservableProperty]
        private int _cargoCapacity = 96;

        [ObservableProperty]
        private ObservableCollection<string> _systems = new() { "All", "Stanton", "Pyro" };

        [ObservableProperty]
        private string _selectedSourceSystem = "All";

        [ObservableProperty]
        private string _selectedTargetSystem = "All";

        [ObservableProperty]
        private ObservableCollection<string> _commodities = new() { "All" };

        [ObservableProperty]
        private string _selectedCommodity = "All";

        [ObservableProperty]
        private double? _minProfit = 0;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private bool _isLoading = false;

        public RouteOptimizerViewModel(IPriceService priceService)
        {
            _priceService = priceService;
            _routeOptimizer = new RouteOptimizerService();

            InitializeShips();
        }


        private void InitializeShips()
        {
            Ships.Add("C2 Hercules (696 SCU)");
            Ships.Add("Caterpillar (576 SCU)");
            Ships.Add("Freelancer MAX (120 SCU)");
            Ships.Add("ARGO RAFT (96 SCU)");
            Ships.Add("Cutlass Black (46 SCU)");
            Ships.Add("Constellation Taurus (174 SCU)");
            Ships.Add("Hull A (64 SCU)");
            Ships.Add("Hull B (384 SCU)");
            Ships.Add("Hull C (4608 SCU)");

            SelectedShip = Ships[3]; // Default to ARGO RAFT
        }

        partial void OnSelectedShipChanged(string value)
        {
            // Extract cargo capacity from ship name
            var startIndex = value.IndexOf('(') + 1;
            var endIndex = value.IndexOf(" SCU");
            if (startIndex > 0 && endIndex > startIndex)
            {
                var capacityStr = value.Substring(startIndex, endIndex - startIndex);
                if (int.TryParse(capacityStr, out int capacity))
                {
                    CargoCapacity = capacity;
                    // Auto-refresh when ship changes
                    _ = RefreshRoutesAsync();
                }
            }
        }

        partial void OnSelectedSourceSystemChanged(string value)
        {
            // Auto-refresh when source system filter changes
            _ = ApplyFiltersAsync();
        }

        partial void OnSelectedTargetSystemChanged(string value)
        {
            // Auto-refresh when target system filter changes
            _ = ApplyFiltersAsync();
        }

        partial void OnSelectedCommodityChanged(string value)
        {
            // Auto-refresh when commodity filter changes
            _ = ApplyFiltersAsync();
        }

        [RelayCommand]
        private async Task RefreshRoutesAsync()
        {
            IsLoading = true;
            StatusText = "Calculating routes...";

            try
            {
                // Get all price data
                var priceData = await _priceService.GetAllCommodityPricesAsync();

                // Build commodity list for filter
                // Only include commodities that can actually be BOUGHT (UnitSellPrice > 0)
                var buyableCommodities = priceData
                    .Where(p => p.UnitSellPrice > 0)
                    .Select(p => p.MineralName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                Commodities.Clear();
                Commodities.Add("All");
                foreach (var commodity in buyableCommodities)
                {
                    Commodities.Add(commodity);
                }

                // Calculate ALL routes
                var allRoutes = _routeOptimizer.CalculateRoutes(priceData, CargoCapacity);

                // Apply filters FIRST
                var filteredRoutes = ApplyFilters(allRoutes);

                // Then sort and take top 100
                var topRoutes = filteredRoutes.OrderByDescending(r => r.TotalProfit).Take(100).ToList();

                // Update UI
                Routes.Clear();
                foreach (var route in topRoutes)
                {
                    Routes.Add(route);
                }

                StatusText = $"Showing {Routes.Count} profitable routes (from {allRoutes.Count} total)";
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ApplyFiltersAsync()
        {
            if (Routes.Count == 0)
                return;

            IsLoading = true;
            try
            {
                // Get all price data
                var priceData = await _priceService.GetAllCommodityPricesAsync();

                // Calculate ALL routes
                var allRoutes = _routeOptimizer.CalculateRoutes(priceData, CargoCapacity);

                // Apply filters FIRST
                var filteredRoutes = ApplyFilters(allRoutes);

                // Then sort and take top 100
                var topRoutes = filteredRoutes.OrderByDescending(r => r.TotalProfit).Take(100).ToList();

                // Update UI
                Routes.Clear();
                foreach (var route in topRoutes)
                {
                    Routes.Add(route);
                }

                StatusText = $"Showing {Routes.Count} profitable routes (from {allRoutes.Count} total)";
            }
            catch (System.Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<TradeRoute> ApplyFilters(List<TradeRoute> routes)
        {
            var filtered = routes.AsEnumerable();

            // Filter by Source System (BuySystem)
            if (!string.IsNullOrEmpty(SelectedSourceSystem) && SelectedSourceSystem != "All")
            {
                filtered = filtered.Where(r => r.BuySystem.Contains(SelectedSourceSystem));
            }

            // Filter by Target System (SellSystem)
            if (!string.IsNullOrEmpty(SelectedTargetSystem) && SelectedTargetSystem != "All")
            {
                filtered = filtered.Where(r => r.SellSystem.Contains(SelectedTargetSystem));
            }

            // Filter by commodity
            if (!string.IsNullOrEmpty(SelectedCommodity) && SelectedCommodity != "All")
            {
                filtered = filtered.Where(r => r.CommodityName == SelectedCommodity);
            }

            // Filter by minimum profit
            if (MinProfit.HasValue && MinProfit.Value > 0)
            {
                filtered = filtered.Where(r => r.TotalProfit >= MinProfit.Value);
            }

            return filtered.ToList();
        }
    }
}
