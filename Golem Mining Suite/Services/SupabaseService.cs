using Golem_Mining_Suite.Models;
using Supabase;
using Supabase.Realtime;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Service for interacting with Supabase backend for live terminal data
    /// </summary>
    public class SupabaseService
    {
        private Client? _client;
        private readonly string _supabaseUrl;
        private readonly string _supabaseKey;
        private bool _isInitialized = false;

        public event EventHandler<TerminalData>? TerminalUpdateReceived;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public SupabaseService(string supabaseUrl, string supabaseKey)
        {
            _supabaseUrl = supabaseUrl;
            _supabaseKey = supabaseKey;
        }

        /// <summary>
        /// Initialize the Supabase client
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                var options = new SupabaseOptions
                {
                    AutoRefreshToken = false,
                    AutoConnectRealtime = true
                };

                _client = new Client(_supabaseUrl, _supabaseKey, options);
                await _client.InitializeAsync();
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] Init failed: {ex.Message}");
                _isInitialized = false;
                return false;
            }
        }

        /// <summary>
        /// Upload terminal data to Supabase
        /// </summary>
        public async Task<bool> UploadTerminalDataAsync(TerminalData data)
        {
            if (!_isInitialized || _client == null)
                return false;

            try
            {
                // Convert to database model
                var dbRecord = new TerminalPriceRecord
                {
                    commodity_name = data.CommodityName,
                    terminal_name = data.TerminalName,
                    star_system = data.StarSystem,
                    price_buy = data.PriceBuy,
                    price_sell = data.PriceSell,
                    inventory_scu = data.InventorySCU,
                    inventory_max = data.InventoryMax,
                    captured_at = data.CapturedAt
                };

                await _client.From<TerminalPriceRecord>().Insert(dbRecord);
                return true;
            }
            catch (Exception ex)
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "livedata_debug.log");
                try 
                { 
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Supabase] Upload failed: {ex.Message}\n");
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Supabase] Exception type: {ex.GetType().Name}\n");
                    if (ex.InnerException != null)
                    {
                        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] [Supabase] Inner exception: {ex.InnerException.Message}\n");
                    }
                } 
                catch { }
                System.Diagnostics.Debug.WriteLine($"[Supabase] Upload failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get recent terminal prices for a commodity
        /// </summary>
        public async Task<List<TerminalData>> GetRecentPricesAsync(string commodityName, int maxAgeMinutes = 30)
        {
            if (!_isInitialized || _client == null)
                return new List<TerminalData>();

            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);

                var response = await _client
                    .From<TerminalPriceRecord>()
                    .Filter("commodity_name", Postgrest.Constants.Operator.Equals, commodityName)
                    .Filter("captured_at", Postgrest.Constants.Operator.GreaterThanOrEqual, cutoffTime.ToString("o"))
                    .Order("captured_at", Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response.Models.Select(r => new TerminalData
                {
                    CommodityName = r.commodity_name ?? string.Empty,
                    TerminalName = r.terminal_name ?? string.Empty,
                    StarSystem = r.star_system ?? string.Empty,
                    PriceBuy = r.price_buy,
                    PriceSell = r.price_sell,
                    InventorySCU = r.inventory_scu,
                    InventoryMax = r.inventory_max,
                    CapturedAt = r.captured_at
                }).ToList();
            }
            catch (Exception)
            {
                return new List<TerminalData>();
            }
        }

        /// <summary>
        /// Subscribe to real-time updates for terminal_prices
        /// </summary>
        public async Task SubscribeToTerminalUpdatesAsync()
        {
            if (!_isInitialized || _client == null) return;

            try
            {
                await _client.Realtime.ConnectAsync();

                var channel = _client.Realtime.Channel("public:terminal_prices");

                // Listen for INSERTs
                channel.On(ChannelEventType.PostgresChanges, (sender, args) =>
                {
                    try
                    {
                         if (args is PostgresChangesResponse change)
                         {
                            var record = change.Payload?.Record;
                            if (record != null)
                            {
                                // Helper to convert dynamic/object to typed object via JSON
                                var json = Newtonsoft.Json.JsonConvert.SerializeObject(record);
                                var dataRecord = Newtonsoft.Json.JsonConvert.DeserializeObject<TerminalPriceRecord>(json);
                                
                                if (dataRecord != null)
                                {
                                     var termData = new TerminalData
                                     {
                                         CommodityName = dataRecord.commodity_name ?? "",
                                         TerminalName = dataRecord.terminal_name ?? "",
                                         StarSystem = dataRecord.star_system ?? "",
                                         PriceBuy = dataRecord.price_buy,
                                         PriceSell = dataRecord.price_sell,
                                         InventorySCU = dataRecord.inventory_scu,
                                         InventoryMax = dataRecord.inventory_max,
                                         CapturedAt = dataRecord.captured_at
                                     };
                                     
                                     TerminalUpdateReceived?.Invoke(this, termData);
                                }
                            }
                         }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Supabase] Error processing update: {ex.Message}");
                    }
                });

                await channel.Subscribe();
                ConnectionStatusChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Supabase] Subscribe failed: {ex.Message}");
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// Get all recent terminal prices (for all commodities)
        /// </summary>
        public async Task<List<TerminalData>> GetAllRecentPricesAsync(int maxAgeMinutes = 30)
        {
            if (!_isInitialized || _client == null)
                return new List<TerminalData>();

            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-maxAgeMinutes);

                var response = await _client
                    .From<TerminalPriceRecord>()
                    .Filter("captured_at", Postgrest.Constants.Operator.GreaterThanOrEqual, cutoffTime.ToString("o"))
                    .Order("captured_at", Postgrest.Constants.Ordering.Descending)
                    .Get();

                return response.Models.Select(r => new TerminalData
                {
                    CommodityName = r.commodity_name ?? string.Empty,
                    TerminalName = r.terminal_name ?? string.Empty,
                    StarSystem = r.star_system ?? string.Empty,
                    PriceBuy = r.price_buy,
                    PriceSell = r.price_sell,
                    InventorySCU = r.inventory_scu,
                    InventoryMax = r.inventory_max,
                    CapturedAt = r.captured_at
                }).ToList();
            }
            catch (Exception)
            {
                return new List<TerminalData>();
            }
        }
    }

    /// <summary>
    /// Database model for terminal_prices table
    /// </summary>
    [Postgrest.Attributes.Table("terminal_prices")]
    public class TerminalPriceRecord : Postgrest.Models.BaseModel
    {
        [Postgrest.Attributes.PrimaryKey("id", false)]
        public int id { get; set; }

        [Postgrest.Attributes.Column("commodity_name")]
        public string? commodity_name { get; set; }

        [Postgrest.Attributes.Column("terminal_name")]
        public string? terminal_name { get; set; }

        [Postgrest.Attributes.Column("star_system")]
        public string? star_system { get; set; }

        [Postgrest.Attributes.Column("price_buy")]
        public int price_buy { get; set; }

        [Postgrest.Attributes.Column("price_sell")]
        public int price_sell { get; set; }

        [Postgrest.Attributes.Column("inventory_scu")]
        public int inventory_scu { get; set; }

        [Postgrest.Attributes.Column("inventory_max")]
        public int inventory_max { get; set; }

        [Postgrest.Attributes.Column("captured_at")]
        public DateTime captured_at { get; set; }

        [Postgrest.Attributes.Column("created_at")]
        public DateTime created_at { get; set; }
    }
}
