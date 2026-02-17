using Golem_Mining_Suite.ViewModels;
using System.Windows;

namespace Golem_Mining_Suite.Windows
{
    public partial class RouteOptimizerWindow : Window
    {
        public RouteOptimizerWindow(RouteOptimizerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Auto-load routes on window open
            Loaded += async (s, e) => await viewModel.RefreshRoutesCommand.ExecuteAsync(null);
        }
    }
}
