using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IPriceService
    {
        Task<List<PriceData>> GetMineralPricesAsync();
        Task<List<PriceData>> GetAllCommodityPricesAsync();
        Task<Dictionary<int, string>> GetTerminalMappingAsync();
        Task<List<TerminalInfo>> GetTerminalsAsync();

        /// <summary>Apply a live-data override for a single terminal/commodity pair.</summary>
        void UpdateWithLiveData(object? sender, TerminalData liveData);

        /// <summary>Reflect the current Realtime connection state for UI indicators.</summary>
        void SetLiveConnectionStatus(bool connected);

        /// <summary>Whether the Supabase Realtime link is currently connected.</summary>
        bool IsLiveConnected { get; }

        /// <summary>Raised when live-data overrides are applied and callers should refresh bound views.</summary>
        event EventHandler? PricesUpdated;

        /// <summary>Raised when the live connection status changes; payload is the new connected state.</summary>
        event EventHandler<bool>? LinkStatusChanged;
    }
}
