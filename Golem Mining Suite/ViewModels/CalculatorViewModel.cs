using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Golem_Mining_Suite.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Golem_Mining_Suite.ViewModels
{
    public partial class CalculatorViewModel : ObservableObject
    {
        private readonly IWindowService _windowService;
        private readonly IPriceService _priceService; // Could use this for live prices if available

        // Initialization Data
        public ObservableCollection<string> Ships { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Stations { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Minerals { get; } = new ObservableCollection<string>();

        // Selections
        [ObservableProperty]
        private string _selectedShip;

        [ObservableProperty]
        private string _selectedStation;

        // Mineral Rows
        public ObservableCollection<MineralRowViewModel> MineralRows { get; } = new ObservableCollection<MineralRowViewModel>();

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
        private string _bestLocationText = "💰 Best Price At: Port Tressler";

        [ObservableProperty]
        private string _pricePerSCUText = "Average: 0 aUEC/SCU";

        // Internal Data Maps
        private readonly Dictionary<string, double> _shipCapacities = new Dictionary<string, double>
        {
            { "Anvil Carrack", 456 },
            { "ARGO RAFT", 96 },
            { "C2 Hercules", 696 },
            { "Caterpillar", 576 },
            { "Constellation Andromeda", 96 },
            { "Constellation Taurus", 174 },
            { "Crusader Spirit C1", 64 },
            { "Cutlass Black", 46 },
            { "Drake Corsair", 72 },
            { "Drake Cutter", 4 },
            { "Freelancer MAX", 120 },
            { "Hull A", 64 },
            { "Hull B", 384 },
            { "Hull C", 4608 },
            { "Mercury Star Runner", 114 },
            { "MISC Starlancer MAX", 224 },
            { "MOLE", 96 },
            { "Origin 400i", 42 },
            { "Prospector", 32 },
            { "ROC", 1.2 },
            { "RSI Zeus Mk II CL", 128 }
        };

        private readonly Dictionary<string, double> _basePrices = new Dictionary<string, double>
        {
            { "Quantanium", 88800 },
            { "Bexalite", 40100 },
            { "Taranite", 36000 },
            { "Borase", 32450 },
            { "Laranite", 30450 },
            { "Agricium", 25550 },
            { "Hephaestanite", 23600 },
            { "Gold", 6204 },
            { "Copper", 6030 },
            { "Beryl", 4930 },
            { "Tungsten", 3825 },
            { "Diamond", 7005 },
            { "Titanium", 8335 },
            { "Corundum", 2525 },
            { "Quartz", 1525 },
            { "Aluminum", 1230 },
            { "Iron", 855 },
            // ROC Minerals
            { "Hadanite", 27500 },
            { "Dolivine", 13500 },
            { "Aphorite", 15250 },
            { "Janalite", 20500 },
            { "Beradom", 11000 },
            { "Feynmaline", 9500 }
        };

        public CalculatorViewModel(IWindowService windowService, IPriceService priceService)
        {
            _windowService = windowService;
            _priceService = priceService;

            InitializeData();

            InitializeData();

            // Ensure selections are not null
            SelectedShip = Ships.FirstOrDefault() ?? "Prospector";
            SelectedStation = Stations.FirstOrDefault() ?? "Default Prices";

            // Initialize with 1 empty row
            AddMineralRow();
        }

        private void InitializeData()
        {
            // Ships
            foreach (var ship in _shipCapacities.Keys.OrderBy(s => s)) Ships.Add(ship);
            SelectedShip = Ships.FirstOrDefault() ?? "Prospector";

            // Stations
            Stations.Add("Default Prices");
            Stations.Add("Port Tressler (+5%)");
            Stations.Add("Port Olisar (Standard)");
            Stations.Add("Everus Harbor (Standard)");
            Stations.Add("Baijini Point (Standard)");
            Stations.Add("Seraphim Station (Standard)");
            SelectedStation = Stations.FirstOrDefault() ?? "Default Prices";

            // Minerals
            Minerals.Add("None");
            foreach (var m in _basePrices.Keys.OrderBy(m => m)) Minerals.Add(m);
        }

        [ObservableProperty]
        private bool _canAddRow = true;

        [RelayCommand]
        private void AddMineralRow()
        {
            if (MineralRows.Count >= 10) return;

            var row = new MineralRowViewModel(this);
            row.PropertyChanged += Row_PropertyChanged;
            MineralRows.Add(row);
            UpdateCanAdd();
            CalculateTotals();
        }

        [RelayCommand]
        private void RemoveMineralRow(MineralRowViewModel row)
        {
             MineralRows.Remove(row);
             UpdateCanAdd();
             CalculateTotals();
        }

        private void UpdateCanAdd()
        {
            CanAddRow = MineralRows.Count < 10;
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
            CargoCapacity = _shipCapacities[SelectedShip];
            UsedCapacity = MineralRows.Sum(r => r.SCU);
            
            CapacityPercentage = CargoCapacity > 0 ? (UsedCapacity / CargoCapacity) * 100 : 0;
            if (CapacityPercentage > 100) CapacityPercentage = 100;

            CapacityText = $"{UsedCapacity:F1} / {CargoCapacity:F1} SCU";
            IsOverCapacity = UsedCapacity > CargoCapacity;

            // Value
            double stationMultiplier = 1.0;
            string bestLocation = "Port Tressler";
            
            if (SelectedStation != null && SelectedStation.Contains("Port Tressler"))
            {
                stationMultiplier = 1.05;
                bestLocation = "Port Tressler";
            }

            double totalValue = 0;
            foreach(var row in MineralRows)
            {
                if (row.SelectedMineral != "None" && _basePrices.ContainsKey(row.SelectedMineral))
                {
                    double price = _basePrices[row.SelectedMineral] * stationMultiplier;
                    totalValue += price * row.SCU;
                }
            }

            TotalValueText = $"Total Value: {totalValue:N0} aUEC";
            BestLocationText = $"💰 Best Price At: {bestLocation}";

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
            _windowService.ShowPricesWindow();
        }
    }

    public partial class MineralRowViewModel : ObservableObject
    {
        private readonly CalculatorViewModel _parent;

        [ObservableProperty]
        private string _selectedMineral = "None";

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

        public ObservableCollection<string> Minerals => _parent.Minerals;

        public MineralRowViewModel(CalculatorViewModel parent)
        {
            _parent = parent;
        }

        partial void OnSelectedMineralChanged(string value) => _parent.CalculateTotals();
        partial void OnScuTextChanged(string value) => _parent.CalculateTotals();

        [RelayCommand]
        private void Clear() // Still useful to clear inputs
        {
            SelectedMineral = "None";
            ScuText = "0";
        }

        [RelayCommand]
        private void Remove()
        {
            _parent.RemoveMineralRowCommand.Execute(this);
        }
    }
}
