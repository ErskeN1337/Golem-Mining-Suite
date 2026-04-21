using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Golem_Mining_Suite.ViewModels; // For MineralRowViewModel potentially or just define new one

namespace Golem_Mining_Suite.ViewModels
{
    public partial class RefineryViewModel : ObservableObject
    {
        private readonly IRefineryService _refineryService;
        private readonly ILogger<RefineryViewModel> _logger;
        // private Dictionary<string, double> _mineralPrices; // Removed unused field

        [ObservableProperty]
        private ObservableCollection<string> _refineries = new();

        [ObservableProperty]
        private string _selectedRefinery = default!;

        [ObservableProperty]
        private ObservableCollection<RefineryMethodOption> _methods = new();

        [ObservableProperty]
        private RefineryMethodOption? _selectedMethod;

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

        // --- 4.7 quality score (single value applied across the calculation) ---
        // Defaulting to 500 keeps the pre-4.7 calculator untouched: Baseline = 1.0x multiplier.
        [ObservableProperty]
        private string _qualityText = "500";

        [ObservableProperty]
        private string _qualityTierText = "Baseline";

        [ObservableProperty]
        private Brush _qualityTierBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xA2, 0x3A));

        [ObservableProperty]
        private string _effectiveValueText = "Effective Value: 0 aUEC";

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

        public RefineryViewModel(IRefineryService refineryService, ILogger<RefineryViewModel> logger)
        {
            _refineryService = refineryService;
            _logger = logger;
            // Fire-and-forget; continuation logs any fault so exceptions don't tear down
            // the finalizer thread the way an unobserved async void would.
            _ = InitializeAsync().ContinueWith(
                t => _logger.LogError(t.Exception, "RefineryViewModel initialization failed"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task InitializeAsync()
        {
            StatusText = "Loading refinery data from UEX Corp API...";
            _allMethods = await _refineryService.GetRefineryMethodsAsync();
            var yields = await _refineryService.GetRefineryYieldsAsync();

            if (_allMethods.Any())
            {
                StatusText = $"Loaded {_allMethods.Count} refinery methods";
                Methods = new ObservableCollection<RefineryMethodOption>(
                    _allMethods.OrderBy(m => m.Name)
                               .Select(m => new RefineryMethodOption { Method = m }));
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

        partial void OnSelectedMethodChanged(RefineryMethodOption? value) => CalculateProfit();
        partial void OnSelectedRefineryChanged(string value) => CalculateProfit();
        partial void OnQualityTextChanged(string value) => CalculateProfit();

        public void CalculateProfit()
        {
            if (SelectedMethod == null || _allMethods == null) return;

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

            var method = SelectedMethod.Method;

            double refineryCost = totalRawValue * (method.CostPercent / 100.0);
            double yieldBonus = method.YieldBonus;
            double refinedValue = totalRawValue * (1 + yieldBonus / 100.0);
            double netProfit = refinedValue - refineryCost;

            // 4.7 quality multiplier applied on top of the refined value so the raw/cost/yield
            // lines keep their pre-4.7 meaning. A default Quality=500 falls in the Baseline tier
            // with a 1.0x multiplier, so this is a no-op for users who ignore the field.
            var quality = TryParseQuality(QualityText);
            UpdateQualityBadge(quality);
            decimal multiplier = Services.RefineryService.QualityMultiplier(quality);
            decimal effectiveRefinedValue = (decimal)refinedValue * multiplier;

            RawValueText = $"Raw Material Value: {totalRawValue:N0} aUEC";
            RefineryCostText = $"Refinery Fee ({method.CostPercent}%): -{refineryCost:N0} aUEC";
            YieldBonusText = $"Yield Bonus: +{yieldBonus:F0}%";
            RefinedValueText = $"Refined Material Value: {refinedValue:N0} aUEC";
            NetProfitText = $"Net Profit: {netProfit:N0} aUEC";
            EffectiveValueText = $"Effective Value ({multiplier:0.##}x): {effectiveRefinedValue:N0} aUEC";
        }

        private static QualityScore? TryParseQuality(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!int.TryParse(text, out int parsed)) return null;
            return new QualityScore(parsed);
        }

        private void UpdateQualityBadge(QualityScore? quality)
        {
            // Null quality → fall back to Baseline styling; we still label it explicitly so the
            // user sees that "nothing entered" is not the same as an 0 quality debuff.
            if (quality is null)
            {
                QualityTierText = "Unknown";
                QualityTierBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                return;
            }

            (string label, Color color) = quality.Value.Tier switch
            {
                QualityTier.Debuff => ("Debuff", Color.FromRgb(0xC1, 0x49, 0x49)),
                QualityTier.Baseline => ("Baseline", Color.FromRgb(0xD4, 0xA2, 0x3A)),
                QualityTier.Good => ("Good", Color.FromRgb(0x67, 0xA9, 0x4F)),
                QualityTier.Keeper => ("Keeper", Color.FromRgb(0x3F, 0x8B, 0x5F)),
                QualityTier.Endgame => ("Endgame", Color.FromRgb(0xA3, 0x7B, 0xD1)),
                _ => ("Baseline", Color.FromRgb(0xD4, 0xA2, 0x3A)),
            };
            QualityTierText = label;
            QualityTierBrush = new SolidColorBrush(color);
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
