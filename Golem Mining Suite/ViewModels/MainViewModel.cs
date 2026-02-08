using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Golem_Mining_Suite.Messages;
using Golem_Mining_Suite.Views;
using Golem_Mining_Suite.Services.Interfaces;
using System;
using System.Diagnostics;
using System.Windows;
using Golem_Mining_Suite.Windows; // For RefineryCalculatorWindow

namespace Golem_Mining_Suite.ViewModels
{
    public partial class MainViewModel : ObservableObject, IRecipient<NavigationMessage>
    {
        private readonly IMiningDataService _miningDataService;
        private readonly IWindowService _windowService;

        // Cache views
        private MainMenuView _mainMenuView;
        private SurfaceMiningView _surfaceMiningView;
        private AsteroidMiningView _asteroidMiningView;
        private ROCMiningView _rocMiningView;

        [ObservableProperty]
        private object _currentView;

        [ObservableProperty]
        private string _versionText;

        public MainViewModel(IMiningDataService miningDataService, IWindowService windowService)
        {
            _miningDataService = miningDataService;
            _windowService = windowService;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";

            // Initialize views
            _mainMenuView = new MainMenuView { DataContext = this };
            
            // SurfaceMiningView now has its own VM, resolved via DI ideally, or set manually here if not refactored fully yet.
            // But wait, if I want to set DataContext to SurfaceMiningViewModel, I need to resolve it.
            // For now, let's keep it simple: Resolve it from App.Services? Or inject IServiceProvider?
            // To avoid circular dependency MainVM <-> SurfaceVM if I injected SurfaceVM here, 
            // I should just use ServiceLocator pattern here temporarily or refactor SurfaceVM creation.
            // Creating SurfaceMiningView here:
            _surfaceMiningView = new SurfaceMiningView();
            // Resolve VM:
            var surfaceVM = App.Current.Services.GetService(typeof(SurfaceMiningViewModel));
            if (surfaceVM != null) _surfaceMiningView.DataContext = surfaceVM;
            
            _asteroidMiningView = new AsteroidMiningView();
            var asteroidVM = App.Current.Services.GetService(typeof(AsteroidMiningViewModel));
            if (asteroidVM != null) _asteroidMiningView.DataContext = asteroidVM;

            _rocMiningView = new ROCMiningView();
            var rocVM = App.Current.Services.GetService(typeof(ROCMiningViewModel));
            if (rocVM != null) _rocMiningView.DataContext = rocVM;

            // Set initial view
            CurrentView = _mainMenuView;

            // Register messenger
            WeakReferenceMessenger.Default.RegisterAll(this);
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
            }
        }

        [RelayCommand]
        private void OpenRefineryCalculator()
        {
            _windowService.ShowRefineryCalculatorWindow();
        }

        [RelayCommand]
        private void OpenPrices()
        {
            _windowService.ShowPricesWindow();
        }

        [RelayCommand]
        private void OpenCalculator()
        {
            _windowService.ShowCalculatorWindow();
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
    }
}
