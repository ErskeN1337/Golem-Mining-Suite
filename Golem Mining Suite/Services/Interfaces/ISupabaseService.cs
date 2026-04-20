using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    /// <summary>
    /// Abstraction over the Supabase backend so callers (LiveDataCoordinator, App startup
    /// wiring, future test doubles) can depend on an interface rather than the concrete
    /// <see cref="Golem_Mining_Suite.Services.SupabaseService"/>.
    /// </summary>
    public interface ISupabaseService
    {
        /// <summary>Raised when a new terminal row is received via Realtime.</summary>
        event EventHandler<TerminalData>? TerminalUpdateReceived;

        /// <summary>Raised when the Realtime socket connects or disconnects.</summary>
        event EventHandler<bool>? ConnectionStatusChanged;

        /// <summary>Initialize the underlying Supabase client. Returns false on failure.</summary>
        Task<bool> InitializeAsync();

        /// <summary>Upload a single captured terminal snapshot.</summary>
        Task<bool> UploadTerminalDataAsync(TerminalData data);

        /// <summary>Recent prices for a single commodity, within <paramref name="maxAgeMinutes"/>.</summary>
        Task<List<TerminalData>> GetRecentPricesAsync(string commodityName, int maxAgeMinutes = 30);

        /// <summary>Recent prices across all commodities, within <paramref name="maxAgeMinutes"/>.</summary>
        Task<List<TerminalData>> GetAllRecentPricesAsync(int maxAgeMinutes = 30);

        /// <summary>Subscribe to Realtime terminal inserts. Long-running; retries internally.</summary>
        Task SubscribeToTerminalUpdatesAsync();
    }
}
