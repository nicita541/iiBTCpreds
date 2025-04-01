using CsvHelper.Configuration.Attributes;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleApp13
{
    internal class BackgroundSender
    {
        private static string ChatIdFilePath = "chat_ids.txt";
        private readonly ITelegramBotClient _botClient;
        private readonly ChartGenerator _chartGenerator;

        public BackgroundSender(ITelegramBotClient botClient, ChartGenerator chartGenerator)
        {
            _botClient = botClient;
            _chartGenerator = chartGenerator;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Фоновая рассылка с точным временем запущена!");

            while (!cancellationToken.IsCancellationRequested)
            {
                //// 1. Ждем до ближайшего интервала
                //var now = DateTime.Now;
                //var nextRun = GetNextRunTime(now);

                //var delay = nextRun - now;
                //Console.WriteLine($"⏳ Ожидание до следующего запуска: {nextRun:HH:mm} (через {delay.Minutes} мин {delay.Seconds} сек)");
                //await Task.Delay(delay, cancellationToken);

                try
                {
                    _chartGenerator.GenerateChartFromCsv();

                    // Получаем последнюю цену из CSV
                    float lastPrice = GetLastPriceFromCsv(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "btc_klines_5m_with_time.csv")); // <- Имя вашего файла с ценами

                    Console.WriteLine($"💲 Последняя цена: {lastPrice:0.00}");

                    Model model = new Model();
                    float[] temp = model.ModelPrediction();
                    bool tempBool = true;
                    string predictionsOutput = "";

                    if (temp[0] == -1) { tempBool = false; }
                    else
                    {
                        predictionsOutput = "📊 Прогнозы и отклонения:\n";

                        for (int i = 0; i < temp.Length; i++)
                        {
                            float deviation = temp[i] - lastPrice;
                            string deviationText = deviation >= 0 ? $"+{deviation:0.00}" : $"{deviation:0.00}";

                            predictionsOutput +=
                                $"{(i + 1) * 5} мин: {temp[i]:0.00} (Отклонение: {deviationText})\n";
                        }
                    }

                    Console.WriteLine("📈 Генерируем график...");
                    // ШАГ 2. Генерировать график и получить путь
                    string chartPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "btc_chart.png");

                    if (!File.Exists(chartPath))
                    {
                        Console.WriteLine("❌ График не найден для отправки.");
                    }
                    else
                    {
                        Console.WriteLine("✅ График готов, начинаем рассылку...");

                        // ШАГ 3. Прочитать chat_id
                        var chatIds = File.Exists(ChatIdFilePath)
                            ? File.ReadAllLines(ChatIdFilePath).Distinct()
                            : Enumerable.Empty<string>();

                        foreach (var chatIdStr in chatIds)
                        {
                            if (long.TryParse(chatIdStr, out long chatId))
                            {
                                try
                                {
                                    using (var stream = new FileStream(chartPath, FileMode.Open, FileAccess.Read))
                                    {
                                        await _botClient.SendPhotoAsync(
                                            chatId: chatId,
                                            photo: new InputFileStream(stream, "btc_chart.png"),
                                            caption: "📊 Новый график за последние 125 минут",
                                            cancellationToken: cancellationToken
                                        );

                                        if (tempBool)
                                        {
                                            // Отправить предсказания и отклонения
                                            await _botClient.SendTextMessageAsync(
                                                chatId: chatId,
                                                text: predictionsOutput,
                                                cancellationToken: cancellationToken
                                            );
                                        }
                                    }

                                    Console.WriteLine($"📤 График успешно отправлен для chat_id: {chatId}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"⚠️ Ошибка при отправке для {chatId}: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❗️ Ошибка фоновой задачи: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Функция для получения последней цены из CSV.
        /// </summary>
        private float GetLastPriceFromCsv(string csvPath)
        {
            try
            {
                if (!File.Exists(csvPath))
                {
                    Console.WriteLine("❌ Файл CSV не найден.");
                    return -1;
                }

                var lastLine = File.ReadLines(csvPath).LastOrDefault();
                if (string.IsNullOrWhiteSpace(lastLine)) return -1;

                // Предположим, что цена находится в первом столбце (можно поменять индекс)
                var parts = lastLine.Split(';', ',', '\t'); // Учитываем разные разделители
                if (parts.Length == 0) return -1;

                if (float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float lastPrice))
                {
                    return lastPrice;
                }

                Console.WriteLine("❌ Не удалось распарсить цену из последней строки.");
                return -1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❗️ Ошибка при чтении CSV: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Функция для получения ближайшего времени, кратного 25 минутам.
        /// </summary>
        private static DateTime GetNextRunTime(DateTime now)
        {
            int minutes = now.Minute;
            int nextMinutes = ((minutes / 25) + 1) * 25;

            int addHours = 0;

            if (nextMinutes >= 60)
            {
                nextMinutes = 0;
                addHours = 1;
            }

            DateTime nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(addHours).AddMinutes(nextMinutes);
            return nextRun;
        }
    }
}
