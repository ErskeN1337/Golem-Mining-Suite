using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Models;
using System.Collections.Generic;
using System.Windows;
using Golem_Mining_Suite.ViewModels; // For MineralRowViewModel potentially or just define new one

namespace Golem_Mining_Suite.ViewModels
{
    public partial class RefineryViewModel : ObservableObject
    {
        private readonly IRefineryService _refineryService;
        // private Dictionary<string, double> _mineralPrices; // Removed unused field

        [ObservableProperty]
        private ObservableCollection<string> _refineries = new();

        [ObservableProperty]
        private string _selectedRefinery = default!;

        [ObservableProperty]
        private ObservableCollection<string> _methods = new();

        [ObservableProperty]
        private string _selectedMethod = default!;

        [ObservableProperty]
        private ObservableCollection<RefineryMineralRowViewModel> _mineralRows = new();

        [ObservableProperty]
        private string _statusText = "Loading...";

        [ObservableProperty]
        private string _rawValueText = "Raw Material Value: 0 aUEC";

        [ObservableProperty]
        private string _refineryCostText = "Refinery Fee: -0 aUEC";

        [ObservableProperty]
        private string _yieldBonusText = "Yield Bonus: +0%";

        [ObservableProperty]
        private string _refinedValueText = "Refined Material Value: 0 aUEC";

        [ObservableProperty]
        private string _netProfitText = "Net Profit: 0 aUEC";

        [ObservableProperty]
        private bool _canAddMineral = true;

        private const int MAX_MINERALS = 10;
        private List<RefineryMethod> _allMethods = new();

        public static readonly Dictionary<string, double> MineralPrices = new Dictionary<string, double>
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
                { "Iron", 855 }
            };

        public RefineryViewModel(IRefineryService refineryService)
        {
            _refineryService = refineryService;
            Initialize();
        }

        private async void Initialize()
        {
            StatusText = "Loading refinery data from UEX Corp API...";
            _allMethods = await _refineryService.GetRefineryMethodsAsync();
            var yields = await _refineryService.GetRefineryYieldsAsync();

            if (_allMethods.Any())
            {
                StatusText = $"Loaded {_allMethods.Count} refinery methods";
                Methods = new ObservableCollection<string>(_allMethods.Select(m => m.Name).OrderBy(n => n));
                if (Methods.Any()) SelectedMethod = Methods.First();
            }
            else
            {
                StatusText = "Using fallback refinery data";
            }

            Refineries = new ObservableCollection<string>(yields.Keys.OrderBy(k => k));
            if (!Refineries.Contains("Default Refinery")) Refineries.Insert(0, "Default Refinery");
            SelectedRefinery = Refineries.First();

            AddMineralRow();
        }

        [RelayCommand]
        private void AddMineralRow()
        {
            if (MineralRows.Count >= MAX_MINERALS)
            {
                MessageBox.Show($"Maximum of {MAX_MINERALS} minerals reached!", "Limit", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var row = new RefineryMineralRowViewModel(this);
            MineralRows.Add(row);
            UpdateCanAdd();
            CalculateProfit();
        }

        [RelayCommand]
        private void RemoveMineralRow(RefineryMineralRowViewModel row)
        {
            MineralRows.Remove(row);
            UpdateCanAdd();
            CalculateProfit();
        }

        private void UpdateCanAdd()
        {
            CanAddMineral = MineralRows.Count < MAX_MINERALS;
        }

        partial void OnSelectedMethodChanged(string value) => CalculateProfit();
        partial void OnSelectedRefineryChanged(string value) => CalculateProfit();

        public void CalculateProfit()
        {
            if (string.IsNullOrEmpty(SelectedMethod) || _allMethods == null) return;

            double totalRawValue = 0;
            
            foreach (var row in MineralRows)
            {
                if (string.IsNullOrEmpty(row.SelectedMineral) || row.SelectedMineral == "None") continue;
                
                if (MineralPrices.ContainsKey(row.SelectedMineral))
                {
                   double price = MineralPrices[row.SelectedMineral];
                   totalRawValue += price * row.Scu;
                }
            }

            var method = _allMethods.FirstOrDefault(m => m.Name == SelectedMethod);
            if (method == null) return;

            double refineryCost = totalRawValue * (method.CostPercent / 100.0);
            double yieldBonus = method.YieldBonus;
            double refinedValue = totalRawValue * (1 + yieldBonus / 100.0);
            double netProfit = refinedValue - refineryCost;

            RawValueText = $"Raw Material Value: {totalRawValue:N0} aUEC";
            RefineryCostText = $"Refinery Fee ({method.CostPercent}%): -{refineryCost:N0} aUEC";
            YieldBonusText = $"Yield Bonus: +{yieldBonus:F0}%";
            RefinedValueText = $"Refined Material Value: {refinedValue:N0} aUEC";
            NetProfitText = $"Net Profit: {netProfit:N0} aUEC";
        }
    }

    public partial class RefineryMineralRowViewModel : ObservableObject
    {
        private readonly RefineryViewModel _parent;

        [ObservableProperty]
        private ObservableCollection<string> _minerals;

        [ObservableProperty]
        private string _selectedMineral;

        [ObservableProperty]
        private string _scuText = "0";

        public double Scu => double.TryParse(ScuText, out var val) ? val : 0;

        public RelayCommand RemoveCommand { get; }

        public RefineryMineralRowViewModel(RefineryViewModel parent)
        {
            _parent = parent;
            Minerals = new ObservableCollection<string>(RefineryViewModel.MineralPrices.Keys.OrderBy(k => k));
            Minerals.Insert(0, "None");
            SelectedMineral = "None";
            RemoveCommand = new RelayCommand(() => _parent.RemoveMineralRowCommand.Execute(this));
        }

        partial void OnSelectedMineralChanged(string value) => _parent.CalculateProfit();
        partial void OnScuTextChanged(string value) => _parent.CalculateProfit();
    }
}
