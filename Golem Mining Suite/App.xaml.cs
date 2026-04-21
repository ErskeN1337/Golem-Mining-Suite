using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
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

            // HTTP — one named client per consumer so headers, base addresses, and timeouts
            // live with service registration rather than being reset inside each call.
            services.AddHttpClient("uex", c =>
            {
                c.BaseAddress = new Uri(UEXService.BaseUrl); // https://api.uexcorp.uk/2.0/
                c.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("prices", c =>
            {
                // PriceService uses absolute URLs (uexcorp.space/api/...), so no BaseAddress here.
                c.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("github", c =>
            {
                c.BaseAddress = new Uri("https://api.github.com/");
                c.Timeout = TimeSpan.FromSeconds(10);
                c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Golem-Mining-Suite", "1.0"));
            });
            services.AddHttpClient("downloads", c =>
            {
                // Binary download of release ZIPs. Keep the factory default (100s) overridden
                // to something long enough for large artifacts on slow connections.
                c.Timeout = TimeSpan.FromMinutes(10);
            });

            // Wave 5A: Regolith importer uses a named client so the api.regolith.rocks base
            // address and 30s timeout live alongside the other HTTP registrations.
            services.AddHttpClient("regolith", c =>
            {
                c.BaseAddress = new Uri("https://api.regolith.rocks/");
                c.Timeout = TimeSpan.FromSeconds(30);
            });

            // Services
            if (secrets.IsSupabaseConfigured)
            {
                services.AddSingleton<ISupabaseService>(p => new SupabaseService(
                    secrets.SupabaseUrl,
                    secrets.SupabaseKey,
                    p.GetRequiredService<ILogger<SupabaseService>>()));
            }
            else
            {
                Log.Warning("Supabase is not configured (no Url/Key via env vars, %APPDATA% override, or appsettings.json). Live data features will be disabled.");
            }

            services.AddSingleton<IMiningDataService, MiningDataService>();
            services.AddSingleton<IPriceService, PriceService>();
            services.AddSingleton<IRefineryService, RefineryService>();
            services.AddSingleton<IWindowService, WindowService>();

            // LiveDataCoordinator tolerates a null ISupabaseService — wire it explicitly so
            // the coordinator is always constructible, but only gets a live Supabase when configured.
            services.AddSingleton<LiveDataCoordinator>(p =>
            {
                var supabase = p.GetService<ISupabaseService>();
                var loggerFactory = p.GetRequiredService<ILoggerFactory>();
                if (supabase == null)
                {
                    loggerFactory.CreateLogger<LiveDataCoordinator>()
                        .LogWarning("LiveDataCoordinator starting without Supabase — live crowdsourced data will not be uploaded.");
                }
                return new LiveDataCoordinator(loggerFactory, supabase);
            });

            // New Services for Hauling
            if (!secrets.IsUexConfigured)
            {
                Log.Warning("UEX API key is not configured. UEX-backed commodity data will be unavailable.");
            }
            services.AddSingleton<UEXService>(p => new UEXService(
                p.GetRequiredService<ILogger<UEXService>>(),
                p.GetRequiredService<IHttpClientFactory>(),
                secrets.UexApiKey));
            services.AddSingleton<ICommodityDataService, CommodityDataService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Update infrastructure — formerly static helpers, now DI-managed so they
            // receive an IHttpClientFactory-configured HttpClient.
            services.AddSingleton<UpdateChecker>();
            services.AddSingleton<AutoUpdater>();

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
            services.AddTransient<RouteOptimizerViewModel>(p => new RouteOptimizerViewModel(
                p.GetRequiredService<IPriceService>(),
                p.GetService<IPiracyRouteAnalyzer>()));
            services.AddSingleton<SettingsViewModel>();

            // Wave 3 services — pure-logic solvers + Game.log tailer.
            services.AddSingleton<FractureSolver>();
            services.AddSingleton<SkipRockPredictor>();
            services.AddSingleton<IGameLogService, GameLogService>();

            // Wave 5A/B/C — Regolith importer, toast service, refinery watcher, crew sessions.
            services.AddSingleton<IRegolithImporter, RegolithImporter>();
            services.AddSingleton<IToastNotificationService, ToastNotificationService>();
            services.AddSingleton<RefineryOrderWatcher>();
            services.AddSingleton<ICrewSessionService, CrewSessionService>();
            services.AddTransient<CrewSessionViewModel>();

            // Wave 6 — counter-piracy analyzer. Singleton because it caches pull-point
            // data for 10 minutes; Supabase is optional so the service is resolved
            // with a nullable fallback (matches LiveDataCoordinator pattern).
            services.AddSingleton<IPiracyRouteAnalyzer>(p => new PiracyRouteAnalyzer(
                p.GetRequiredService<ILogger<PiracyRouteAnalyzer>>(),
                p.GetService<ISupabaseService>()));

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

            // Wire up Live Data events — both sides are now behind interfaces, so no
            // `is PriceService ps` cast is needed.
            var supabase = Services.GetService<ISupabaseService>();
            var priceService = Services.GetRequiredService<IPriceService>();

            if (supabase != null)
            {
                supabase.TerminalUpdateReceived += priceService.UpdateWithLiveData;
                supabase.ConnectionStatusChanged += (s, connected) => priceService.SetLiveConnectionStatus(connected);

                // Start listening if configured
                // DISABLED FOR RELEASE: Feature Flagged off until ready
                // _ = supabase.SubscribeToTerminalUpdatesAsync();
            }

            // Fire-and-forget: start tailing Game.log. Safe when SC isn't installed —
            // the service logs a warning and stays disarmed.
            var gameLog = Services.GetRequiredService<IGameLogService>();
            _ = gameLog.StartAsync().ContinueWith(
                t => Log.Error(t.Exception, "GameLogService failed to start."),
                TaskContinuationOptions.OnlyOnFaulted);

            // Wave 5B: load persisted crew sessions so the UI has data ready when the
            // user opens the Crew Sessions view. Non-fatal on failure — the service logs
            // and starts with an empty list.
            var crewSessionService = Services.GetRequiredService<ICrewSessionService>();
            _ = crewSessionService.LoadAsync().ContinueWith(
                t => Log.Warning(t.Exception, "CrewSessionService failed to load at startup."),
                TaskContinuationOptions.OnlyOnFaulted);

            // Wave 5C: refinery watcher ctor loads from disk synchronously, so no
            // explicit LoadAsync is needed — resolving the singleton primes it.
            _ = Services.GetRequiredService<RefineryOrderWatcher>();

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
