using System;
using System.IO;

namespace NBPExchangeRatesLib
{

    public static class FileExporter
    {
        public static bool SaveToFile(ExchangeRateSeries series, string fileName, FileFormat format, bool includeHeadersInCsv=true)
        {
            if (fileName == null || fileName.Length == 0) { return false; }

            string content;

            switch (format)
            {
                case FileFormat.csv:
                    content = ExchangeRateSeriesFormatter.FormatAsCsv(series, includeHeadersInCsv);
                    break;
                case FileFormat.txt:
                    content = ExchangeRateSeriesFormatter.FormatAsText(series);
                    break;
                default:
                    return false;
            }
            

            try
            {
                File.WriteAllText(fileName, content);
                return true;
            }
            catch
            {
                return false;
            }
            
        }

        public static string EnsureFileExtension(string fileName, FileFormat format)
        {
            string extension = $".{format}";
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                fileName += extension;
            }
            return fileName;
        }
    }
}
