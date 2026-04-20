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
    }
}
