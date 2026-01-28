using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Services;

/// <summary>
/// Simple NBP API Client for exchange rates
/// </summary>
public class NbpExchangeRatesApi
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.nbp.pl/api/exchangerates/rates/a";

    public NbpExchangeRatesApi()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// Get latest exchange rate for a currency
    /// </summary>
    /// <param name="currencyCode">Currency code (USD, EUR, etc.)</param>
    /// <returns>Exchange rate as double</returns>
    public double GetLatestExchangeRateDouble(string currencyCode)
    {
        var url = $"{BaseUrl}/{currencyCode.ToUpper()}/?format=json";
        
        try
        {
            var response = _httpClient.GetAsync(url).Result;
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"NBP API returned status code: {response.StatusCode}");
            }

            var json = response.Content.ReadAsStringAsync().Result;
            var result = JsonSerializer.Deserialize<NbpApiResponse>(json);

            if (result?.Rates == null || result.Rates.Count == 0)
            {
                throw new Exception("No exchange rate data returned from NBP API");
            }

            return result.Rates[0].Mid;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error fetching exchange rate from NBP API: {ex.Message}", ex);
        }
    }

    private class NbpApiResponse
    {
        [JsonPropertyName("table")]
        public string? Table { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("rates")]
        public List<NbpRate>? Rates { get; set; }
    }

    private class NbpRate
    {
        [JsonPropertyName("no")]
        public string? No { get; set; }

        [JsonPropertyName("effectiveDate")]
        public string? EffectiveDate { get; set; }

        [JsonPropertyName("mid")]
        public double Mid { get; set; }
    }
}
