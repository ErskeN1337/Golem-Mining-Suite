using System.Collections.Generic;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IPriceService
    {
        Task<List<PriceData>> GetMineralPricesAsync();
        Task<Dictionary<int, string>> GetTerminalMappingAsync();
    }
}
