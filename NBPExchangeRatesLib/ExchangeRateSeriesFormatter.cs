using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBPExchangeRatesLib
{
    public static class ExchangeRateSeriesFormatter
    {

        public static string FormatAsText(ExchangeRateSeries series)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Waluta: {series.Currency}");
            sb.AppendLine($"Kod waluty: {series.Code}");
            sb.AppendLine($"Typ tabeli: {series.Table}");
            sb.AppendLine();
            sb.AppendLine("Kursy walut:");

            foreach (var rate in series.Rates)
            {
                sb.AppendLine($"Data: {rate.EffectiveDate:yyyy-MM-dd}, Kurs średni: {rate.Mid} PLN, Numer tabeli: {rate.No}");
            }

            return sb.ToString();
        }

        public static string FormatAsCsv(ExchangeRateSeries series, bool includeHeaders=true)
        {
            var sb = new StringBuilder();

            if (includeHeaders)
            {
                sb.AppendLine("Data,Kurs średni,Numer tabeli,Kod waluty,Nazwa waluty");
            }

            foreach (var rate in series.Rates)
            {
                sb.AppendLine($"{rate.EffectiveDate:yyyy-MM-dd},{rate.Mid.ToString(CultureInfo.InvariantCulture)},{rate.No},{series.Code},{series.Currency}");
            }

            return sb.ToString();
        }
    }
}
