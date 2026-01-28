using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBPExchangeRatesLib
{
    public class ExchangeRateSeries
    {
        public string Table { get; set; }
        public string Currency { get; set; }
        public string Code { get; set; }
        public List<ExchangeRate> Rates { get; set; } = new List<ExchangeRate>();
    }
    public class ExchangeRate
    {
        public string No { get; set; }
        public DateTime EffectiveDate { get; set; }
        public decimal Mid { get; set; }
    }
}
