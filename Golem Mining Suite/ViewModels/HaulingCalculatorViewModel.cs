using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class HaulingCalculatorViewModel : ObservableObject
    {
        private readonly IWindowService _windowService;
        private readonly IPriceService _priceService;
        
        // Cache of prices: CommodityName -> (StationName -> Price)
        private Dictionary<string, Dictionary<string, double>> _commodityPrices = new Dictionary<string, Dictionary<string, double>>();

        // Initialization Data
        public ObservableCollection<string> Ships { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Stations { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Commodities { get; } = new ObservableCollection<string>();

        // Selections
        [ObservableProperty]
        private string _selectedShip;

        [ObservableProperty]
        private string _selectedStation;

        // Commodity Rows
        public ObservableCollection<HaulingCommodityRowViewModel> Rows { get; } = new ObservableCollection<HaulingCommodityRowViewModel>();

        // Results
        [ObservableProperty]
        private double _cargoCapacity;

        [ObservableProperty]
        private double _usedCapacity;

        [ObservableProperty]
        private double _capacityPercentage;

        [ObservableProperty]
        private string _capacityText = "0 / 0 SCU";

        [ObservableProperty]
        private bool _isOverCapacity;

        [ObservableProperty]
        private string _totalValueText = "Total Value: 0 aUEC";
        
        [ObservableProperty]
        private string _profitText = "Estimated Profit: TBD"; 

        [ObservableProperty]
        private string _pricePerSCUText = "Average: 0 aUEC/SCU";

        // Ship Capacities
        private readonly Dictionary<string, double> _shipCapacities = new Dictionary<string, double>
        {
            { "C2 Hercules", 696 },
            { "Caterpillar", 576 },
            { "M2 Hercules", 522 },
            { "Carrack", 456 },
            { "Starfarer", 291 },
            { "Constellation Taurus", 174 },
            { "Freelancer MAX", 120 },
            { "Cutlass Black", 46 },
            { "Hull C", 4608 }, 
            { "Hull A", 64 },
            { "Raft", 96 }
        };

        public HaulingCalculatorViewModel(IWindowService windowService, IPriceService priceService)
        {
            _windowService = windowService;
            _priceService = priceService;

            InitializeData();

            // Initialize 1 row
            AddCommodityRow();
        }

        private async void InitializeData()
        {
            // Ships
            foreach (var ship in _shipCapacities.Keys.OrderBy(s => s)) Ships.Add(ship);
            SelectedShip = Ships.FirstOrDefault(s => s == "C2 Hercules") ?? Ships.FirstOrDefault();

            // Load Commodities & Prices
            var prices = await _priceService.GetAllCommodityPricesAsync();
            
            _commodityPrices.Clear();
            Commodities.Clear();
            Stations.Clear();
            Commodities.Add("None");

            var allStations = new HashSet<string>();

            // Organize data: Commodity -> Station -> Price
            foreach (var p in prices)
            {
                if (!string.IsNullOrEmpty(p.BestLocation))
                {
                    if (!_commodityPrices.ContainsKey(p.MineralName))
                        _commodityPrices[p.MineralName] = new Dictionary<string, double>();

                    // Check if we already have this station for this commodity (avoid dups if API returns multiple)
                    if (!_commodityPrices[p.MineralName].ContainsKey(p.BestLocation))
                    {
                        _commodityPrices[p.MineralName][p.BestLocation] = p.NumericPrice;
                    }
                    else
                    {
                        // Keep highest if duplicate?
                        if (p.NumericPrice > _commodityPrices[p.MineralName][p.BestLocation])
                             _commodityPrices[p.MineralName][p.BestLocation] = p.NumericPrice;
                    }

                    allStations.Add(p.BestLocation);
                }
            }

            // Populate Stations List
            foreach(var s in allStations.OrderBy(s => s))
            {
                Stations.Add(s);
            }
            
            // Should we set a default station? Maybe the one with the most commodities?
            // or just the first one alphabetically?
            SelectedStation = Stations.FirstOrDefault();

            // Populate Commodities List (All available commodities)
            foreach (var c in _commodityPrices.Keys.OrderBy(k => k))
            {
                Commodities.Add(c);
            }
        }

        [ObservableProperty]
        private bool _canAddRow = true;

        [RelayCommand]
        private void AddCommodityRow()
        {
            if (Rows.Count >= 10) return;

            var row = new HaulingCommodityRowViewModel(this);
            row.PropertyChanged += Row_PropertyChanged;
            Rows.Add(row);
            UpdateCanAdd();
            CalculateTotals();
        }

        [RelayCommand]
        private void RemoveCommodityRow(HaulingCommodityRowViewModel row)
        {
             Rows.Remove(row);
             UpdateCanAdd();
             CalculateTotals();
        }

        private void UpdateCanAdd()
        {
            CanAddRow = Rows.Count < 10;
        }

        partial void OnSelectedShipChanged(string value) => CalculateTotals();
        partial void OnSelectedStationChanged(string value) => CalculateTotals();

        private void Row_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            CalculateTotals();
        }

        public void CalculateTotals()
        {
            if (string.IsNullOrEmpty(SelectedShip)) return;

            // Capacity
            if (_shipCapacities.TryGetValue(SelectedShip, out double cap)) CargoCapacity = cap;
            else CargoCapacity = 0;

            UsedCapacity = Rows.Sum(r => r.SCU);
            
            CapacityPercentage = CargoCapacity > 0 ? (UsedCapacity / CargoCapacity) * 100 : 0;
            if (CapacityPercentage > 100) CapacityPercentage = 100;

            CapacityText = $"{UsedCapacity:F1} / {CargoCapacity:F1} SCU";
            IsOverCapacity = UsedCapacity > CargoCapacity;

            // Value Calculation based on Selected Station
            double totalValue = 0;
            
            if (!string.IsNullOrEmpty(SelectedStation))
            {
                foreach(var row in Rows)
                {
                    if (row.SelectedCommodity != "None" && _commodityPrices.ContainsKey(row.SelectedCommodity))
                    {
                        // Check if this station buys/sells this commodity
                        var stationPrices = _commodityPrices[row.SelectedCommodity];
                        if (stationPrices.ContainsKey(SelectedStation))
                        {
                            double price = stationPrices[SelectedStation];
                            totalValue += price * row.SCU;
                        }
                        else
                        {
                            // Station does not trade this commodity? Or we don't have data.
                            // Value is 0 for this part.
                        }
                    }
                }
            }

            TotalValueText = $"Total Value: {totalValue:N0} aUEC";

            if (UsedCapacity > 0)
            {
                double avg = totalValue / UsedCapacity;
                PricePerSCUText = $"Average: {avg:N0} aUEC/SCU";
            }
            else
            {
                PricePerSCUText = "Average: 0 aUEC/SCU";
            }
        }

        [RelayCommand]
        private void OpenComparison()
        {
            // _windowService.ShowHaulingPricesWindow(); // Future method
        }
    }

    public partial class HaulingCommodityRowViewModel : ObservableObject
    {
        private readonly HaulingCalculatorViewModel _parent;

        [ObservableProperty]
        private string _selectedCommodity = "None";

        [ObservableProperty]
        private string _scuText = "0";

        public double SCU 
        { 
            get 
            {
                if (double.TryParse(ScuText, out double val)) return val;
                return 0;
            } 
        }

        public ObservableCollection<string> Commodities => _parent.Commodities;

        public HaulingCommodityRowViewModel(HaulingCalculatorViewModel parent)
        {
            _parent = parent;
        }

        partial void OnSelectedCommodityChanged(string value) => _parent.CalculateTotals();
        partial void OnScuTextChanged(string value) => _parent.CalculateTotals();

        [RelayCommand]
        private void Clear()
        {
            SelectedCommodity = "None";
            ScuText = "0";
        }

        [RelayCommand]
        private void Remove()
        {
            _parent.RemoveCommodityRowCommand.Execute(this);
        }
    }
}
