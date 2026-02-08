using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Golem_Mining_Suite.ViewModels;

namespace Golem_Mining_Suite
{
    public partial class LocationWindow : Window
    {
        public LocationWindow(string name, bool isMineralSearch, bool asteroidMode, bool rocMode = false)
        {
            InitializeComponent();

            // Resolve ViewModel from DI container
            var viewModel = App.Current.Services.GetRequiredService<LocationViewModel>();
            
            // Initialize with parameters
            viewModel.Initialize(name, isMineralSearch, asteroidMode, rocMode);
            
            // Set DataContext
            DataContext = viewModel;
        }
    }
}