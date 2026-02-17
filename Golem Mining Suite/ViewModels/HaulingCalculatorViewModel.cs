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
        
        // Cache of prices: CommodityName -> (StationName -> PriceData)
        // Storing PriceData instead of double to access both Buy/Sell prices
        internal Dictionary<string, Dictionary<string, PriceData>> _commodityPrices = new Dictionary<string, Dictionary<string, PriceData>>();

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
            
            // Ensure selections are not null
            SelectedShip = Ships.FirstOrDefault(s => s == "C2 Hercules") ?? Ships.FirstOrDefault() ?? "C2 Hercules";
            SelectedStation = Stations.FirstOrDefault() ?? "Default Station";

            // Initialize 1 row
            AddCommodityRow();
        }

        private async void InitializeData()
        {
            // Ships
            foreach (var ship in _shipCapacities.Keys.OrderBy(s => s)) Ships.Add(ship);
            SelectedShip = Ships.FirstOrDefault(s => s == "C2 Hercules") ?? Ships.FirstOrDefault() ?? "C2 Hercules";

            // Load Commodities & Prices
            var prices = await _priceService.GetAllCommodityPricesAsync();
            
            _commodityPrices.Clear();
            Commodities.Clear();
            Stations.Clear();
            Commodities.Add("None");

            var allStations = new HashSet<string>();

            // Organize data: Commodity -> Station -> PriceData
            foreach (var p in prices)
            {
                if (!string.IsNullOrEmpty(p.BestLocation))
                {
                    if (!_commodityPrices.ContainsKey(p.MineralName))
                        _commodityPrices[p.MineralName] = new Dictionary<string, PriceData>();

                    var stationDict = _commodityPrices[p.MineralName];

                    if (!stationDict.ContainsKey(p.BestLocation))
                    {
                        stationDict[p.BestLocation] = p;
                        allStations.Add(p.BestLocation);
                    }
                    else
                    {
                        // META-FIX: Merge data if we have multiple entries for the same station name
                        // (e.g. multiple terminals at same location, or API artifacts)
                        // We want the BEST prices available to the user.
                        var existing = stationDict[p.BestLocation];
                        
                        if (p.UnitBuyPrice > existing.UnitBuyPrice) 
                            existing.UnitBuyPrice = p.UnitBuyPrice; // Found a terminal that pays more
                            
                        if (p.UnitSellPrice < existing.UnitSellPrice && p.UnitSellPrice > 0)
                             existing.UnitSellPrice = p.UnitSellPrice; // Found a terminal that sells cheaper? (Actually we want max for availability usually, but min for cost)
                        else if (existing.UnitSellPrice == 0 && p.UnitSellPrice > 0)
                             existing.UnitSellPrice = p.UnitSellPrice;

                        // Also update numeric price for sorting if needed
                        existing.NumericPrice = Math.Max(existing.NumericPrice, p.NumericPrice);
                    }
                }
            }

            // Populate Stations List
            foreach(var s in allStations.OrderBy(s => s))
            {
                Stations.Add(s);
            }
            
            // Default station
            SelectedStation = Stations.FirstOrDefault() ?? "Default Station";

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

            // Update price display for all rows (in case station changed)
            foreach (var row in Rows)
            {
                row.UpdatePriceDisplay();
            }

            // Value Calculation based on Selected Station
            double totalValue = 0;
            
            if (!string.IsNullOrEmpty(SelectedStation))
            {
                foreach(var row in Rows)
                {
                    if (row.SelectedCommodity != "None" && _commodityPrices.ContainsKey(row.SelectedCommodity))
                    {
                        // Check if this station buys/sells this commodity
                        var stationData = _commodityPrices[row.SelectedCommodity];
                        if (stationData.ContainsKey(SelectedStation))
                        {
                            var priceData = stationData[SelectedStation];
                            
                            // Haul Value = How much the station PAYS (UnitBuyPrice)
                            // Multiplier: 1 SCU = 100 Units
                            // If UnitBuyPrice is 0 (Station doesn't buy), then value is 0.
                            double unitPrice = priceData.UnitBuyPrice;
                            
                            totalValue += unitPrice * row.SCU * 100; 
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

        [ObservableProperty]
        private string _pricePerUnit = "";

        [ObservableProperty]
        private string _rowValue = "";

        [ObservableProperty]
        private string _priceWarning = "";

        [ObservableProperty]
        private bool _hasPriceWarning = false;

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

        partial void OnSelectedCommodityChanged(string value)
        {
            UpdatePriceDisplay();
            _parent.CalculateTotals();
        }

        partial void OnScuTextChanged(string value)
        {
            UpdatePriceDisplay();
            _parent.CalculateTotals();
        }

        public void UpdatePriceDisplay()
        {
            if (string.IsNullOrEmpty(_parent.SelectedStation) || SelectedCommodity == "None")
            {
                PricePerUnit = "";
                RowValue = "";
                PriceWarning = "";
                HasPriceWarning = false;
                return;
            }

            // Check if we have price data for this commodity at the selected station
            if (_parent._commodityPrices.ContainsKey(SelectedCommodity))
            {
                var stationData = _parent._commodityPrices[SelectedCommodity];
                if (stationData.ContainsKey(_parent.SelectedStation))
                {
                    var priceData = stationData[_parent.SelectedStation];
                    double unitPrice = priceData.UnitBuyPrice;

                    // Always show the price, even if it's 0
                    double pricePerSCU = unitPrice * 100;
                    PricePerUnit = $"{pricePerSCU:N0} aUEC/SCU";
                    
                    double rowVal = unitPrice * SCU * 100;
                    RowValue = $"Value: {rowVal:N0} aUEC";
                    
                    PriceWarning = "";
                    HasPriceWarning = false;
                }
                else
                {
                    // No price data for this specific station - show 0
                    PricePerUnit = "0 aUEC/SCU";
                    RowValue = "Value: 0 aUEC";
                    PriceWarning = "";
                    HasPriceWarning = false;
                }
            }
            else
            {
                // Commodity not in price list
                PricePerUnit = "";
                RowValue = "";
                PriceWarning = "";
                HasPriceWarning = false;
            }
        }

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
