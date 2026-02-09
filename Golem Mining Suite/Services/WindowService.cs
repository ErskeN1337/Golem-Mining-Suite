using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Golem_Mining_Suite.ViewModels; // Added this
using Golem_Mining_Suite.Windows;

namespace Golem_Mining_Suite.Services
{
    public class WindowService : Interfaces.IWindowService
    {
        private readonly IServiceProvider _serviceProvider;

        public WindowService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void ShowPricesWindow()
        {
            var window = new PricesWindow();
            var vm = _serviceProvider.GetService<PricesViewModel>();
            if (vm != null) window.DataContext = vm;
            PositionAndShow(window);
        }

        public void ShowCalculatorWindow()
        {
            var window = new CalculatorWindow();
            var vm = _serviceProvider.GetService<CalculatorViewModel>();
            if (vm != null) window.DataContext = vm;
            PositionAndShow(window);
        }

        public void ShowRefineryCalculatorWindow()
        {
            var window = new RefineryCalculatorWindow();
            var vm = _serviceProvider.GetService<RefineryViewModel>();
            if (vm != null) window.DataContext = vm;
            PositionAndShow(window);
        }

        public void ShowLocationWindow(string name, bool isMineral, bool isAsteroid, bool isRoc)
        {
            // LocationWindow resolves its VM internally or we can do it here if we refactor it further
            var window = new LocationWindow(name, isMineral, isAsteroid, isRoc);
            PositionAndShow(window);
        }

        private void PositionAndShow(Window window)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.PositionWindowToRight(window);
                window.Owner = mainWindow;
            }
            window.Show();
        }
    }
}
