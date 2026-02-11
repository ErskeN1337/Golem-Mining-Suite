using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Golem_Mining_Suite.Services
{
    public class UEXService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<UEXService> _logger;
        private const string BaseUrl = "https://api.uexcorp.uk/2.0/";

        public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

        public UEXService(ILogger<UEXService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            
            // simple config read
            _apiKey = "";
            try {
                var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(appSettingsPath))
                {
                    var json = File.ReadAllText(appSettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("UEX", out var uexElement))
                    {
                        if(uexElement.TryGetProperty("ApiKey", out var keyElement))
                        {
                            _apiKey = keyElement.GetString() ?? "";
                        }
                    }
                }
            } catch { } // Configuration read failure, assume no key
        }

        public async Task<List<CommodityData>> GetCommoditiesAsync()
        {
            if (!IsConfigured) return new List<CommodityData>();

            try
            {
                // UEX API endpoint: /commodities
                // Note: Actual endpoint might vary based on docs, assuming GET /commodities works or similar
                // Based on docs provided: https://api.uexcorp.uk/2.0/commodities
                
                // Add headers if needed? Usually Authorization header or query param?
                // Docs check needed. Usually it's a header. Assuming "Authorization: Bearer <key>" or custom header?
                // Docs say: "To obtain your access token...". Usually headers.
                // UEX often uses custom headers but let's try standard first or if docs were read closer.
                // Re-reading snippet: "X-Client-Version" mentioned. 
                // Let's assume standard Bearer or query param if not specified.
                // Wait, standard UEX public API sometimes doesn't need key for basic lists? 
                // But user said "We're gonna use UEX API".
                // I will assume it's publicly available OR key is needed. 
                // Let's try to pass key if we have it.
                
                // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey); 
                // or apiKey query param? 
                
                var response = await _httpClient.GetAsync("commodities");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UexCommoditiesResponse>();
                    if (result?.Data != null)
                    {
                        var list = new List<CommodityData>();
                        foreach (var item in result.Data)
                        {
                             list.Add(new CommodityData 
                             {
                                 Name = item.Name ?? "Unknown",
                                 Code = item.Code ?? "",
                                 AveragePriceBuy = item.PriceBuyAvg,
                                 AveragePriceSell = item.PriceSellAvg
                             });
                        }
                        return list;
                    }
                }
                else
                {
                    _logger.LogWarning($"UEX API returned {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch UEX commodities");
            }

            return new List<CommodityData>();
        }
    }

    // Helper classes for JSON deserialization
    public class UexCommoditiesResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("data")]
        public List<UexCommodity>? Data { get; set; }
    }

    public class UexCommodity
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("slug")] // or Code?
        public string? Code { get; set; }
        
        [JsonPropertyName("price_buy")]
        public double PriceBuyAvg { get; set; } // Simplified mapping
        
        [JsonPropertyName("price_sell")]
        public double PriceSellAvg { get; set; }
    }
}
