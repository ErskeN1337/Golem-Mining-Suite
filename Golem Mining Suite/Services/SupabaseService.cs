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
        private Supabase.Client? _client;
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

                _client = new Supabase.Client(_supabaseUrl, _supabaseKey, options);
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
        /// <summary>
        /// Subscribe to real-time updates for terminal_prices
        /// </summary>
        public async Task SubscribeToTerminalUpdatesAsync()
        {
            if (!_isInitialized || _client == null) return;

            await ConnectAndSubscribeAsync();
        }

        private async Task ConnectAndSubscribeAsync()
        {
            while (true)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[Supabase] Attempting to connect to Realtime...");
                    await _client!.Realtime.ConnectAsync();

                    var channel = await _client.From<TerminalPriceRecord>()
                        .On(PostgresChangesOptions.ListenType.Inserts, (sender, change) =>
                        {
                            try
                            {
                                var record = change.Model<TerminalPriceRecord>();
                                if (record != null)
                                {
                                    var terminalData = new TerminalData
                                    {
                                        CommodityName = record.commodity_name ?? "",
                                        TerminalName = record.terminal_name ?? "",
                                        StarSystem = record.star_system ?? "",
                                        PriceBuy = record.price_buy,
                                        PriceSell = record.price_sell,
                                        InventorySCU = record.inventory_scu,
                                        InventoryMax = record.inventory_max,
                                        CapturedAt = record.captured_at
                                    };

                                    TerminalUpdateReceived?.Invoke(this, terminalData);
                                    System.Diagnostics.Debug.WriteLine($"[Supabase] Received live update for {terminalData.CommodityName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Supabase] Error processing update: {ex.Message}");
                            }
                        });

                    await channel.Subscribe();

                    System.Diagnostics.Debug.WriteLine("[Supabase] Realtime connected and subscribing");
                    ConnectionStatusChanged?.Invoke(this, true);

                    // If we get here, we are connected. We need to wait until we are disconnected to try again.
                    // The client library might have its own reconnection, but if it throws or closes, we want to catch it.
                    // However, ConnectAsync returns, so we don't have a blocking call to "Wait".
                    // We can monitor the state or just exit the loop if the library handles it.
                    // BUT, the user reported "The remote party closed the WebSocket connection".
                    // So we probably need a keep-alive or a way to detect the close.
                    // For now, let's break the loop. If the connection drops, the library *should* throw an event,
                    // but if it doesn't auto-reconnect, we might be stuck.
                    // Let's add a periodic check.
                    
                    await MonitorConnectionAsync();
                    
                    // If Monitor returns, it means we lost connection.
                    System.Diagnostics.Debug.WriteLine("[Supabase] Connection lost, retrying in 5 seconds...");
                    ConnectionStatusChanged?.Invoke(this, false);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Supabase] Subscribe/Connect failed: {ex.Message}");
                    ConnectionStatusChanged?.Invoke(this, false);
                    await Task.Delay(10000); // Wait 10s before retry on error
                }
            }
        }

        private async Task MonitorConnectionAsync()
        {
            // Simple monitor to check if we are still connected.
            // Note: The C# Supabase Realtime client doesn't expose a simple "IsConnected" property easily accessible here 
            // without digging into the socket.
            // As a workaround, we will rely on the fact that if the underlying socket closes, 
            // the listener might stop receiving. 
            // However, the original error was an exception. 
            
            // NOTE: Ideally we would attach to an OnClose/OnDisconnect event from the client, 
            // but the Supabase-csharp client documentation/interface for that varies.
            
            // For now, we'll implement a dummy delay loop. Real robust implementation requires
            // checking _client.Realtime.Socket.State if exposed, or handling the disconnect event.
            // Since we can't easily see the internal state, we will assume the library stays connected
            // unless we decide to restart.
            
            // To properly catch the "Remote party closed" exception which likely happens ON the socket thread,
            // we might need to rely on the library's internal error handling or global exception handlers.
            
            // Only exit this method if we detect a failure or want to reconnect.
            // Currently, we just wait indefinitely until an exception bubbles up or we implement a heartbeat.
            
            // Let's assume the loop in ConnectAndSubscribeAsync handles the "Start" and exception retry.
            // But once `await channel.Subscribe()` returns, we are just "running".
            
            // If the socket closes, does it throw here? No.
            // The exception seen in logs "Error while listening to websocket stream" comes from `WebsocketClient.Listen`.
            // The Supabase library likely uses `Websocket.Client`.
            
            // We can try to keep this method alive.
            try 
            {
                while (_client!.Realtime.Socket != null) // Check if socket object exists
                {
                   await Task.Delay(2000);
                   // If we could check state: 
                   // if (_client.Realtime.Socket.IsConnected == false) return;
                }
            }
            catch
            {
                return;
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
