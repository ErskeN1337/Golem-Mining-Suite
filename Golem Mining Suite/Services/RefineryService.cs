using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Golem_Mining_Suite.Services
{
    public class RefineryService : IRefineryService
    {
        private readonly Dictionary<string, RefineryMethod> _refineryMethods = new();
        private readonly Dictionary<string, Dictionary<string, double>> _refineryYields = new();

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RefineryService> _logger;

        public RefineryService(IHttpClientFactory httpClientFactory, ILogger<RefineryService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<RefineryMethod>> GetRefineryMethodsAsync()
        {
            if (_refineryMethods.Count > 0) return new List<RefineryMethod>(_refineryMethods.Values);

            try
            {
                var client = _httpClientFactory.CreateClient("uex");
                var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_methods");
                var jsonDoc = JsonDocument.Parse(response);
                var methods = jsonDoc.RootElement.GetProperty("data");

                foreach (var method in methods.EnumerateArray())
                {
                    string? name = method.GetProperty("name").GetString();
                    string? code = method.GetProperty("code").GetString();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(code)) continue;

                    int yieldRating = method.GetProperty("rating_yield").GetInt32();
                    int costRating = method.GetProperty("rating_cost").GetInt32();
                    int speedRating = method.GetProperty("rating_speed").GetInt32();

                    // Calculate percentages based on ratings
                    double yieldPercent = yieldRating == 3 ? 70 : (yieldRating == 2 ? 50 : 30);
                    double costPercent = costRating == 3 ? 15 : (costRating == 2 ? 10 : 7);

                    _refineryMethods[name] = new RefineryMethod
                    {
                        Name = name,
                        Code = code,
                        YieldBonus = yieldPercent,
                        CostPercent = costPercent,
                        YieldRating = yieldRating,
                        CostRating = costRating,
                        SpeedRating = speedRating
                    };
                }
            }
            catch (Exception)
            {
                LoadFallbackRefineryData();
            }

            return new List<RefineryMethod>(_refineryMethods.Values);
        }

        public async Task<Dictionary<string, Dictionary<string, double>>> GetRefineryYieldsAsync()
        {
            if (_refineryYields.Count > 0) return _refineryYields;

            try
            {
                var client = _httpClientFactory.CreateClient("uex");
                var response = await client.GetStringAsync("https://api.uexcorp.uk/2.0/refineries_yields");
                var jsonDoc = JsonDocument.Parse(response);
                var yields = jsonDoc.RootElement.GetProperty("data");

                foreach (var yieldData in yields.EnumerateArray())
                {
                    string? terminal = yieldData.GetProperty("terminal_name").GetString();
                    string? commodity = yieldData.GetProperty("commodity_name").GetString();
                    int value = yieldData.GetProperty("value").GetInt32();

                    if (string.IsNullOrEmpty(terminal) || string.IsNullOrEmpty(commodity)) continue;

                    if (!_refineryYields.ContainsKey(terminal))
                    {
                        _refineryYields[terminal] = new Dictionary<string, double>();
                    }

                    _refineryYields[terminal][commodity] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch UEX refinery yields; returning whatever was cached");
            }

            return _refineryYields;
        }

        private void LoadFallbackRefineryData()
        {
            _refineryMethods["Dinyx Solvents"] = new RefineryMethod { Name = "Dinyx Solvents", Code = "DINYX", YieldBonus = 70, CostPercent = 15 };
            _refineryMethods["Cormack Method"] = new RefineryMethod { Name = "Cormack Method", Code = "CORMACK", YieldBonus = 50, CostPercent = 10 };
            _refineryMethods["XCR Reaction"] = new RefineryMethod { Name = "XCR Reaction", Code = "XCR", YieldBonus = 30, CostPercent = 7 };
        }
    }
}
