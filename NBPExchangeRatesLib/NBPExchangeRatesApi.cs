using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;


namespace NBPExchangeRatesLib
{
    [Guid("5DDC1C13-7DE1-4835-BCDF-A3C9F74DE41C")]
    [ComVisible(true)]
    public interface INbpExchangeRatesApi
    {
        ExchangeRateSeries GetLatestExchangeRate(string currency, FetchFormat format = FetchFormat.XML);
        ExchangeRateSeries GetDateExchangeRate(string currency, DateTime date, FetchFormat format = FetchFormat.XML);
        ExchangeRateSeries GetDateExchangeRate(string currency, string dateString, FetchFormat format = FetchFormat.XML);
        ExchangeRateSeries GetDateRangeExchangeRate(string currency, DateTime startDate, DateTime endDate, FetchFormat format = FetchFormat.XML);
        ExchangeRateSeries GetDateRangeExchangeRate(string currency, string startDateString, string endDateString, FetchFormat format = FetchFormat.XML);
    }
    [Guid("669C5543-FDC5-4782-AFA0-D32C6963D9B3")]
    [ComVisible(true)]
    public class NbpExchangeRatesApi : INbpExchangeRatesApi
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.nbp.pl/api/exchangerates/rates/a/";

        public NbpExchangeRatesApi()
        {
            _httpClient = new HttpClient();
        }

        public Double GetLatestExchangeRateDouble(string currency)
        {
            return Decimal.ToDouble(GetLatestExchangeRate(currency).Rates[0].Mid);
        }

        public ExchangeRateSeries GetLatestExchangeRate(string currency, FetchFormat format = FetchFormat.XML)
        {
            string url = $"{BaseUrl}{currency}/?format={format.ToString().ToLower()}";
            string rawData = FetchData(url);

            return DeserializeData(rawData, format);
        }

        public ExchangeRateSeries GetDateExchangeRate(string currency, DateTime date, FetchFormat format = FetchFormat.XML)
        {
            string dateStr = date.ToString("yyyy-MM-dd");
            return GetDateExchangeRate(currency, dateStr, format);
        }

        public ExchangeRateSeries GetDateExchangeRate(string currency, string dateString, FetchFormat format = FetchFormat.XML)
        {
            string url = $"{BaseUrl}{currency}/{dateString}/?format={format.ToString().ToLower()}";
            string rawData = FetchData(url);

            return DeserializeData(rawData, format);
        }

        public ExchangeRateSeries GetDateRangeExchangeRate(string currency, DateTime startDate, DateTime endDate, FetchFormat format = FetchFormat.XML)
        {
            string startDateStr = startDate.ToString("yyyy-MM-dd");
            string endDateStr = endDate.ToString("yyyy-MM-dd");

            return GetDateRangeExchangeRate(currency, startDateStr, endDateStr, format);
        }

        public ExchangeRateSeries GetDateRangeExchangeRate(string currency, string startDateString, string endDateString, FetchFormat format = FetchFormat.XML)
        {
            string url = $"{BaseUrl}{currency}/{startDateString}/{endDateString}/?format={format.ToString().ToLower()}";
            string rawData = FetchData(url);

            return DeserializeData(rawData, format);
        }

        private string FetchData(string url)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;

            using (var client = new WebClient())
            {
                string response = client.DownloadString(url);
                if (!string.IsNullOrEmpty(response))
                {
                    return response;
                }
            }

            return "No data";
        }

        private static bool ValidateServerCertificate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
        {
            // Custom validation logic here
            return true; // Always accept for this example
        }

        private ExchangeRateSeries DeserializeData(string data, FetchFormat format)
        {
            try
            {
                if (format == FetchFormat.XML)
                {
                    return DeserializeXml(data);
                }
                else
                {
                    return DeserializeJson(data);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd deserializacji danych: {ex.Message}");
            }
        }

        private ExchangeRateSeries DeserializeXml(string xmlData)
        {
            var serializer = new XmlSerializer(typeof(NbpExchangeRateResponse));
            using (var reader = new StringReader(xmlData))
            {
                var response = (NbpExchangeRateResponse)serializer.Deserialize(reader);
                return ConvertToExchangeRateSeries(response);
            }
        }

        private ExchangeRateSeries DeserializeJson(string jsonData)
        {
            var response = JsonConvert.DeserializeObject<NbpExchangeRateResponse>(jsonData);
            return ConvertToExchangeRateSeries(response);
        }

        private ExchangeRateSeries ConvertToExchangeRateSeries(NbpExchangeRateResponse response)
        {
            var result = new ExchangeRateSeries
            {
                Table = response.Table,
                Currency = response.Currency,
                Code = response.Code,
                Rates = new List<ExchangeRate>()
            };

            foreach (var rate in response.Rates)
            {
                result.Rates.Add(new ExchangeRate
                {
                    No = rate.No,
                    EffectiveDate = DateTime.Parse(rate.EffectiveDate),
                    Mid = rate.Mid
                });
            }

            return result;
        }
    }

    [XmlRoot("ExchangeRatesSeries")]
    public class NbpExchangeRateResponse
    {
        [XmlElement("Table")]
        [JsonProperty("table")]
        public string Table { get; set; }

        [XmlElement("Currency")]
        [JsonProperty("currency")]
        public string Currency { get; set; }

        [XmlElement("Code")]
        [JsonProperty("code")]
        public string Code { get; set; }

        [XmlArray("Rates")]
        [XmlArrayItem("Rate")]
        [JsonProperty("rates")]
        public List<NbpRate> Rates { get; set; }
    }
    public class NbpRate
    {
        [XmlElement("No")]
        [JsonProperty("no")]
        public string No { get; set; }

        [XmlElement("EffectiveDate")]
        [JsonProperty("effectiveDate")]
        public string EffectiveDate { get; set; }

        [XmlElement("Mid")]
        [JsonProperty("mid")]
        public decimal Mid { get; set; }
    }
}