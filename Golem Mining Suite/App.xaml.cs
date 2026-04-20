using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using Golem_Mining_Suite.Services;
using Golem_Mining_Suite.Services.Configuration;
using Golem_Mining_Suite.Services.Interfaces;
using Golem_Mining_Suite.ViewModels;
using Golem_Mining_Suite.Views; // For views if needed, though MainWindow is in root
using Serilog;

namespace Golem_Mining_Suite
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; private set; }

        public App()
        {
            InitializeComponent();
            // Services initialized in OnStartup to ensure Resources are loaded
            Services = new ServiceCollection().BuildServiceProvider(); // Default empty provider to satisfy non-nullable
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Logging — initialise Serilog early so SecretResolver diagnostics can be logged.
            // Kept as the static Log.Logger because the UnhandledException handler has no DI.
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            services.AddLogging(b => b.AddSerilog(dispose: true));

            // Configuration — layered: env vars > %APPDATA%\Golem Mining Suite\appsettings.json > shipped appsettings.json
            var secrets = SecretResolver.Resolve();

            // Services
            if (secrets.IsSupabaseConfigured)
            {
                services.AddSingleton<SupabaseService>(
                    _ => new SupabaseService(secrets.SupabaseUrl, secrets.SupabaseKey));
            }
            else
            {
                Log.Warning("Supabase is not configured (no Url/Key via env vars, %APPDATA% override, or appsettings.json). Live data features will be disabled.");
            }

            services.AddSingleton<IMiningDataService, MiningDataService>();
            services.AddSingleton<IPriceService, PriceService>();
            services.AddSingleton<IRefineryService, RefineryService>();
            services.AddSingleton<IWindowService, WindowService>();

            // LiveDataCoordinator tolerates a null SupabaseService — wire it explicitly so
            // the coordinator is always constructible, but only gets a live Supabase when configured.
            services.AddSingleton<LiveDataCoordinator>(p =>
            {
                var supabase = p.GetService<SupabaseService>();
                if (supabase == null)
                {
                    var log = p.GetService<ILogger<LiveDataCoordinator>>();
                    log?.LogWarning("LiveDataCoordinator starting without Supabase — live crowdsourced data will not be uploaded.");
                }
                return new LiveDataCoordinator(supabase);
            });

            // New Services for Hauling
            if (!secrets.IsUexConfigured)
            {
                Log.Warning("UEX API key is not configured. UEX-backed commodity data will be unavailable.");
            }
            services.AddSingleton<UEXService>(p => new UEXService(
                p.GetRequiredService<ILogger<UEXService>>(),
                secrets.UexApiKey));
            services.AddSingleton<ICommodityDataService, CommodityDataService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SurfaceMiningViewModel>();
            services.AddSingleton<AsteroidMiningViewModel>();
            services.AddSingleton<ROCMiningViewModel>();
            services.AddSingleton<HaulingDashboardViewModel>(); // Register new VM
            services.AddSingleton<LiveDataViewModel>(p => new LiveDataViewModel(
                p.GetRequiredService<LiveDataCoordinator>(),
                p.GetRequiredService<IPriceService>()));
            services.AddTransient<LocationViewModel>();
            services.AddTransient<PricesViewModel>();
            services.AddTransient<CalculatorViewModel>();
            services.AddTransient<RefineryViewModel>();
            services.AddTransient<HaulingPricesViewModel>();
            services.AddTransient<HaulingCalculatorViewModel>();
            services.AddTransient<RouteOptimizerViewModel>();
            services.AddSingleton<SettingsViewModel>();

            // Windows
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Global exception handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            Services = ConfigureServices();
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
                // DISABLED FOR RELEASE: Feature Flagged off until ready
                // _ = supabase.SubscribeToTerminalUpdatesAsync();
            }

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled Dispatcher Exception");
            
            // Prevent crash for non-critical UI errors if desired, but for now let's just log and maybe alert
            // e.Handled = true; 
            
            MessageBox.Show($"An unhandled error occurred: {e.Exception.Message}\n\nCheck logs for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled AppDomain Exception");
                MessageBox.Show($"A fatal error occurred: {ex.Message}\n\nCheck logs for details.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
