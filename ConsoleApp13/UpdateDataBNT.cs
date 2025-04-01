using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace ConsoleApp13
{
    public static class UpdateDataBNT
    {
        private static readonly HttpClient client = new HttpClient();
        private const string Url = "https://api.bybit.com/v5/market/kline";

        public static async Task<List<List<object>>> GetKlinesAsync(string symbol, string interval, long startTime, long endTime, int limit = 1000)
        {
            var queryString = $"?category=linear&symbol={symbol}&interval={interval}&start={startTime}&end={endTime}&limit={limit}";
            var url = Url + queryString;

            Console.WriteLine("Отправляем запрос: " + url);  // Логируем URL запроса
            var response = await client.GetStringAsync(url);

            var data = JObject.Parse(response);

            if ((int)data["retCode"] != 0)
            {
                Console.WriteLine("Ошибка запроса: " + data);
                return new List<List<object>>();
            }

            return data["result"]["list"].ToObject<List<List<object>>>();
        }


        public static async Task startUpload()
        {
            string symbol = "BTCUSDT";
            string interval = "5";  // Интервал 5 минут
            int limit = 125;  // максимум 200 свечей
            int totalMinutesNeeded = 125;  // Например, 100000 минут

            // Текущее время и округление вниз до ближайших 5 минут
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long fiveMinutes = 5 * 60 * 1000;

            // Округляем текущее время вниз до ближайших 5 минут
            long currentTimeRounded = (currentTime / fiveMinutes) * fiveMinutes;

            // Стартуем с этого времени назад на totalMinutesNeeded
            long startTime = currentTimeRounded - totalMinutesNeeded * 60 * 1000;  // Перевод в миллисекунды

            var totalData = new List<List<object>>();

            while (startTime < currentTimeRounded)
            {
                long endTime = startTime + limit * fiveMinutes;
                if (endTime > currentTimeRounded)
                {
                    endTime = currentTimeRounded;
                }

                var klines = await GetKlinesAsync(symbol, interval, startTime, endTime, limit);
                totalData.AddRange(klines);

                Console.WriteLine($"Получено {klines.Count} свечей с {DateTimeOffset.FromUnixTimeMilliseconds(startTime).UtcDateTime} по {DateTimeOffset.FromUnixTimeMilliseconds(endTime).UtcDateTime}");

                // Переход к следующему диапазону
                startTime = endTime;

                await Task.Delay(100);  // Пауза для защиты от rate limit
            }

            Console.WriteLine($"Всего собрано свечей: {totalData.Count}");

            // ✅ Сохранение в CSV с отображением времени
            using (var writer = new StreamWriter("btc_klines_5m_with_time.csv"))
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                csv.WriteField("timestamp");
                csv.WriteField("open");
                csv.WriteField("high");
                csv.WriteField("low");
                csv.WriteField("close");
                csv.WriteField("volume");
                csv.WriteField("turnover");
                csv.WriteField("time");
                csv.NextRecord();

                foreach (var kline in totalData)
                {
                    long timestamp = Convert.ToInt64(kline[0]);
                    string timeStr = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");

                    foreach (var field in kline)
                    {
                        csv.WriteField(field);
                    }
                    csv.WriteField(timeStr);
                    csv.NextRecord();
                }
            }

            Console.WriteLine("Данные сохранены в btc_klines_5m_with_time.csv");
        }
    }
}
