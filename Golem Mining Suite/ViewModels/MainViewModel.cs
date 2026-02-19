using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Golem_Mining_Suite.Messages;
using Golem_Mining_Suite.Views;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.Models;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
// using Golem_Mining_Suite.Windows; // Removed unused


namespace Golem_Mining_Suite.ViewModels
{
    public enum AppMode
    {
        Mining,
        Hauling
    }

    public partial class MainViewModel : ObservableObject, IRecipient<NavigationMessage>
    {
        private readonly IMiningDataService _miningDataService;
        private readonly IWindowService _windowService;



        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HomeButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(SurfaceButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(AsteroidButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(ROCButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(IsMiningMode))]
        [NotifyPropertyChangedFor(nameof(IsHaulingMode))]
        private AppMode _currentMode = AppMode.Mining;

        public bool IsMiningMode => CurrentMode == AppMode.Mining;
        public bool IsHaulingMode => CurrentMode == AppMode.Hauling;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HomeButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(SurfaceButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(AsteroidButtonVisibility))]
        [NotifyPropertyChangedFor(nameof(ROCButtonVisibility))]
        private object _currentView;

        [ObservableProperty]
        private string _versionText;

        public Visibility HomeButtonVisibility => CurrentView is MainMenuView ? Visibility.Collapsed : Visibility.Visible;
        
        public Visibility SurfaceButtonVisibility => IsMiningMode && !(CurrentView is SurfaceMiningView) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AsteroidButtonVisibility => IsMiningMode && !(CurrentView is AsteroidMiningView) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ROCButtonVisibility => IsMiningMode && !(CurrentView is ROCMiningView) ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty]
        private string _welcomeText = "Welcome to Golem Mining Suite";

        [ObservableProperty]
        private string _logoSource = "/Assets/Images/Golem Mining Suite Logo.png";

        [ObservableProperty]
        private double _logoHeight = 145;

        [RelayCommand]
        private void SwitchToMining()
        {
            CurrentMode = AppMode.Mining;
            CurrentView = _mainMenuView;
            UpdateBranding();
        }

        [RelayCommand]
        private void SwitchToHauling()
        {
            CurrentMode = AppMode.Hauling;
            CurrentView = _mainMenuView;
            UpdateBranding();
        }

        private void UpdateBranding()
        {
            if (IsMiningMode)
            {
                WelcomeText = "Welcome to Golem Mining Suite";
                LogoSource = "/Assets/Images/Golem Mining Suite Logo.png";
                LogoHeight = 145;
                LogoMargin = new Thickness(15, -40, 15, 0);
                
                // Set Mining theme accent color (Orange) if Auto
                if (SettingsVM.SelectedTheme.Value == "Auto")
                {
                    Application.Current.Resources["AccentColor"] = (Color)ColorConverter.ConvertFromString("#FF8C42");
                    Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8C42"));
                }
            }
            else
            {
                WelcomeText = "Welcome to Golem Hauling Suite";
                LogoSource = "pack://siteoforigin:,,,/Assets/Images/Golem Hauling Suite Logo.png";
                LogoHeight = 145;
                LogoMargin = new Thickness(15, -40, 15, 0);
                
                // Set Hauling theme accent color (Blue) if Auto
                if (SettingsVM.SelectedTheme.Value == "Auto")
                {
                    Application.Current.Resources["AccentColor"] = (Color)ColorConverter.ConvertFromString("#4A90E2");
                    Application.Current.Resources["AccentBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2"));
                }
            }
        }

        [ObservableProperty]
        private Thickness _logoMargin = new Thickness(15, -40, 15, 0);

        private readonly LiveDataCoordinator _liveDataCoordinator;
        private readonly IPriceService _priceService;

        // Cache views
        private MainMenuView _mainMenuView;
        private SurfaceMiningView _surfaceMiningView;
        private AsteroidMiningView _asteroidMiningView;
        private ROCMiningView _rocMiningView;
        private SettingsView _settingsView;
        private HaulingDashboardView _haulingDashboardView; // Cached view
        private HaulingRoutesView _haulingRoutesView;
        private WalletView _walletView;


        [ObservableProperty]
        private bool _isLocationPromptVisible;
        
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<TerminalInfo> _locationPromptTerminals = new();

        [ObservableProperty]
        private TerminalInfo? _selectedLocationPromptTerminal;

        public SettingsViewModel SettingsVM { get; }
        public WalletViewModel WalletVM { get; }

        public MainViewModel(IMiningDataService miningDataService, IWindowService windowService, LiveDataViewModel liveDataVM, LiveDataCoordinator coordinator, IPriceService priceService, SettingsViewModel settingsVM, WalletViewModel walletVM)
        {
            _miningDataService = miningDataService;
            _windowService = windowService;
            _liveDataCoordinator = coordinator;
            _priceService = priceService;
            SettingsVM = settingsVM;
            WalletVM = walletVM;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
                VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";
            else
                VersionText = "v1.0.0";
            
            // Initialize HaulingRoutesView to avoid null warning
            _haulingRoutesView = new HaulingRoutesView();

            // Register popup request handler
            _liveDataCoordinator.LocationRequired += OnLocationRequired;

            // Initialize views
            _mainMenuView = new MainMenuView { DataContext = this };
            
            _surfaceMiningView = new SurfaceMiningView();
            var surfaceVM = App.Current.Services.GetService(typeof(SurfaceMiningViewModel));
            if (surfaceVM != null) _surfaceMiningView.DataContext = surfaceVM;
            
            _asteroidMiningView = new AsteroidMiningView();
            var asteroidVM = App.Current.Services.GetService(typeof(AsteroidMiningViewModel));
            if (asteroidVM != null) _asteroidMiningView.DataContext = asteroidVM;

            _rocMiningView = new ROCMiningView();
            var rocVM = App.Current.Services.GetService(typeof(ROCMiningViewModel));
            if (rocVM != null) _rocMiningView.DataContext = rocVM;
            
            _haulingDashboardView = new HaulingDashboardView();
            var haulingVM = App.Current.Services.GetService(typeof(HaulingDashboardViewModel));
            if (haulingVM != null) _haulingDashboardView.DataContext = haulingVM;

            _settingsView = new SettingsView();
            _settingsView.DataContext = settingsVM;

            _walletView = new WalletView();
            _walletView.DataContext = walletVM;

            // Set initial view
            CurrentView = _mainMenuView;

            // Register messenger
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private void OnLocationRequired(object? sender, EventArgs e)
        {
            // Marshal to UI thread
            Application.Current.Dispatcher.Invoke(async () =>
            {
                if (IsLocationPromptVisible) return; // Already showing

                // Load terminals if empty
                if (LocationPromptTerminals.Count == 0)
                {
                    var terminals = await _priceService.GetTerminalsAsync();
                    foreach (var t in terminals) LocationPromptTerminals.Add(t);
                }

                IsLocationPromptVisible = true;
                
                // Bring app to front if possible? 
                // Application.Current.MainWindow.Activate();
            });
        }

        [RelayCommand]
        private void ConfirmLocation()
        {
            if (SelectedLocationPromptTerminal != null)
            {
                _liveDataCoordinator.SetManualLocation(SelectedLocationPromptTerminal.Name, SelectedLocationPromptTerminal.StarSystem);
                IsLocationPromptVisible = false;
            }
        }

        [RelayCommand]
        private void CancelLocation()
        {
            IsLocationPromptVisible = false;
        }

        public void Receive(NavigationMessage message)
        {
            Navigate(message.Value);
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            switch (destination)
            {
                case "MainMenu":
                    CurrentView = _mainMenuView;
                    break;
                case "SurfaceMining":
                    CurrentView = _surfaceMiningView;
                    break;
                case "AsteroidMining":
                    CurrentView = _asteroidMiningView;
                    break;
                case "ROCMining":
                    CurrentView = _rocMiningView;
                    break;
                case "Settings":
                    CurrentView = _settingsView;
                    break;
                case "Wallet":
                    CurrentView = _walletView;
                    break;
            }
        }

        [RelayCommand]
        private void OpenRefineryCalculator()
        {
            _windowService.ShowRefineryCalculatorWindow();
        }

        [RelayCommand]
        private void OpenCalculator()
        {
            _windowService.ShowCalculatorWindow();
        }

        [RelayCommand]
        private void OpenHaulingCalculator()
        {
            _windowService.ShowHaulingCalculatorWindow();
        }

        [RelayCommand]
        private void OpenRouteOptimizer()
        {
            _windowService.ShowRouteOptimizerWindow();
        }

        [RelayCommand]
        private void OpenPrices()
        {
            _windowService.ShowPricesWindow();
        }

        [RelayCommand]
        private void OpenHaulingPrices()
        {
            _windowService.ShowHaulingPricesWindow();
        }

        [RelayCommand]
        private void OpenHaulingRoutes()
        {
             CurrentView = _haulingRoutesView;
        }

        [RelayCommand]
        private void OpenUexLink()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://uexcorp.space",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open UEX Corp website: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenBugReport()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ErskeN1337/Golem-Mining-Suite/issues/new",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open GitHub Issues: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
