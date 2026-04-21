using Golem_Mining_Suite.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Windows;

namespace Golem_Mining_Suite.Windows
{
    public partial class RouteOptimizerWindow : Window
    {
        private readonly RouteOptimizerViewModel _viewModel;
        private readonly ILogger<RouteOptimizerWindow>? _logger;

        public RouteOptimizerWindow(RouteOptimizerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            _logger = App.Current?.Services?.GetService<ILogger<RouteOptimizerWindow>>();

            // Auto-load routes on window open. Task returned from the command is
            // observed via a continuation so an exception no longer leaks as an
            // async void through the Loaded event handler.
            Loaded += (s, e) =>
            {
                _ = _viewModel.RefreshRoutesCommand.ExecuteAsync(null).ContinueWith(
                    t => _logger?.LogError(t.Exception, "RouteOptimizer auto-refresh on load failed"),
                    TaskContinuationOptions.OnlyOnFaulted);
            };
        }
    }
}
