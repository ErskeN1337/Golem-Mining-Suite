using Golem_Mining_Suite.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services.Interfaces
{
    public interface IRefineryService
    {
        Task<List<RefineryMethod>> GetRefineryMethodsAsync();
        Task<Dictionary<string, Dictionary<string, double>>> GetRefineryYieldsAsync();
    }
}
