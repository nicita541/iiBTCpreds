using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp; // Убедитесь, что пакет OxyPlot.SkiaSharp установлен

namespace ConsoleApp13
{
    public class ChartGenerator
    {
        // НЕ МЕНЯЕМ, как вы просили:
        private static string csvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "btc_klines_5m_with_time.csv");
        // НЕ МЕНЯЕМ, как вы просили:
        private static string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "btc_chart.png");

        public void GenerateChartFromCsv()
        {
            UpdateDataBNT.startUpload();

            // 1. Считываем данные из CSV и удаляем лишние строки после 26-й строки
            List<Candle> candles = ReadCandlesFromCsv(csvFilePath);

            // 2. Если нет данных, завершаем программу
            if (candles.Count == 0)
            {
                Console.WriteLine("Нет данных для графика!");
                return;
            }

            // 3. Сортируем данные по времени
            candles = candles.OrderBy(c => c.Time).ToList();

            // 4. Создаем модель для графика
            var plotModel = new PlotModel
            {
                Title = "BTC 5m (Свечной график)",
                Background = OxyColors.White // Устанавливаем фон графика
            };

            // 5. Добавляем оси: X – DateTime, Y – цены
            plotModel.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm",
                Title = "Время",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });
            plotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Цена (USDT)",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            // 6. Создаем серию японских свечей
            var candleStickSeries = new CandleStickSeries
            {
                DecreasingColor = OxyColors.Red,
                IncreasingColor = OxyColors.Green
            };

            // 7. Добавляем все свечи
            foreach (var c in candles)
            {
                double xValue = DateTimeAxis.ToDouble(c.Time);
                candleStickSeries.Items.Add(
                    new HighLowItem(
                        x: xValue,
                        high: c.High,
                        low: c.Low,
                        open: c.Open,
                        close: c.Close
                    )
                );
            }

            plotModel.Series.Add(candleStickSeries);

            // 8. Экспортируем картинку в PNG
            using (var stream = File.Create(filePath))
            {
                var exporter = new PngExporter
                {
                    Width = 1600, // Увеличиваем ширину для улучшения четкости
                    Height = 1000, // Увеличиваем высоту для улучшения четкости
                };
                exporter.Export(plotModel, stream);
            }

            Console.WriteLine($"График сохранён: {filePath}");
        }

        /// <summary>
        /// Считывает данные из CSV-файла.
        /// Предполагается, что первая строка - заголовок:
        /// timestamp,open,high,low,close,volume,turnover,time
        /// </summary>
        private static List<Candle> ReadCandlesFromCsv(string filePath)
        {
            var result = new List<Candle>();
            if (!File.Exists(filePath))
                return result;

            var allLines = File.ReadAllLines(filePath);

            // Берем заголовок + первые 25 строк
            var trimmedLines = allLines.Take(26).ToArray(); // 1 заголовок + 25 строк данных

            File.WriteAllLines(filePath, trimmedLines);
            Console.WriteLine($"Обрезанный файл успешно сохранён: {filePath}");

            Thread.Sleep(300); // даём ОС "догнать" и освободить файл

            // Теперь парсим эти trimmedLines для дальнейшей работы
            foreach (var line in trimmedLines.Skip(1)) // Пропускаем заголовок
            {
                var parts = line.Split(',');
                if (parts.Length < 8)
                    continue;

                try
                {
                    var candle = new Candle
                    {
                        Timestamp = long.Parse(parts[0], CultureInfo.InvariantCulture),
                        Open = double.Parse(parts[1], CultureInfo.InvariantCulture),
                        High = double.Parse(parts[2], CultureInfo.InvariantCulture),
                        Low = double.Parse(parts[3], CultureInfo.InvariantCulture),
                        Close = double.Parse(parts[4], CultureInfo.InvariantCulture),
                        Volume = double.Parse(parts[5], CultureInfo.InvariantCulture),
                        Turnover = double.Parse(parts[6], CultureInfo.InvariantCulture),
                        Time = DateTime.ParseExact(parts[7], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    };
                    result.Add(candle);
                }
                catch
                {
                    // Пропускаем некорректные строки
                }
            }
            return result;
        }


        /// <summary>
        /// Класс свечи (Candle) для хранения строки CSV
        /// </summary>
        public class Candle
        {
            public long Timestamp { get; set; }
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public double Volume { get; set; }
            public double Turnover { get; set; }
            public DateTime Time { get; set; }
        }
    }
}