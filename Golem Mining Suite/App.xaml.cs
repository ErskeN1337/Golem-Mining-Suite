using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.ViewModels;
using Golem_Mining_Suite.Views; // For views if needed, though MainWindow is in root
using Serilog;

namespace Golem_Mining_Suite
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<IMiningDataService, MiningDataService>();
            services.AddSingleton<IPriceService, PriceService>();
            services.AddSingleton<IRefineryService, RefineryService>();
            services.AddSingleton<IWindowService, WindowService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SurfaceMiningViewModel>();
            services.AddSingleton<AsteroidMiningViewModel>();
            services.AddSingleton<ROCMiningViewModel>();
            services.AddTransient<LocationViewModel>();
            services.AddTransient<PricesViewModel>();
            services.AddTransient<CalculatorViewModel>();
            services.AddTransient<RefineryViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();

            // Logging
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            
            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
