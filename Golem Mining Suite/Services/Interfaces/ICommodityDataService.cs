using System.Collections.Generic;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface ICommodityDataService
    {
        Task<List<CommodityData>> GetAllCommoditiesAsync();
        Task<CommodityData?> GetCommodityDetailsAsync(string commodityName);
    }
}
