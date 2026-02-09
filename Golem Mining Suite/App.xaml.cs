using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.ViewModels;
using Golem_Mining_Suite.Views; // For views if needed, though MainWindow is in root
using Serilog;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

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

            // Configuration
            string supabaseUrl = "";
            string supabaseKey = "";
            try {
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    var json = File.ReadAllText(appSettingsPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Supabase", out var supabaseElement))
                    {
                        supabaseUrl = supabaseElement.GetProperty("Url").GetString() ?? "";
                        supabaseKey = supabaseElement.GetProperty("Key").GetString() ?? "";
                    }
                }
            } catch { }

            // Services
            if (!string.IsNullOrEmpty(supabaseUrl) && !string.IsNullOrEmpty(supabaseKey))
            {
                services.AddSingleton<SupabaseService>(p => new SupabaseService(supabaseUrl, supabaseKey));
            }
            
            services.AddSingleton<IMiningDataService, MiningDataService>();
            services.AddSingleton<IPriceService, PriceService>();
            services.AddSingleton<IRefineryService, RefineryService>();
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<LiveDataCoordinator>(p => new LiveDataCoordinator(p.GetService<SupabaseService>())); // Register Coordinator with factory

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SurfaceMiningViewModel>();
            services.AddSingleton<AsteroidMiningViewModel>();
            services.AddSingleton<ROCMiningViewModel>();
            services.AddSingleton<LiveDataViewModel>(); // Register LiveDataVM
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

            // Wire up Live Data events
            var supabase = Services.GetService<SupabaseService>();
            var priceService = Services.GetRequiredService<IPriceService>();
            
            if (supabase != null)
            {
                supabase.TerminalUpdateReceived += (s, data) => 
                {
                    if (priceService is PriceService ps) ps.UpdateWithLiveData(s, data);
                };
                
                supabase.ConnectionStatusChanged += (s, connected) => 
                {
                    if (priceService is PriceService ps) ps.SetLiveConnectionStatus(connected);
                };
                
                // Start listening if configured
                _ = supabase.SubscribeToTerminalUpdatesAsync();
            }

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}
