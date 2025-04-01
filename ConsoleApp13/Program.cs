using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ConsoleApp13
{
    class Program
    {
        private static string BotToken = "7759664866:AAFymDIUUNtGpp3VYLYMMncCo9hFyejiHH0";
        private static TelegramBotClient Bot;
        public static readonly DateTime BotStartTime = DateTime.UtcNow;

        static async Task Main()
        {
            Bot = new TelegramBotClient(BotToken);
            var me = await Bot.GetMeAsync();
            Console.WriteLine($"Бот запущен: @{me.Username}");

            using var cts = new CancellationTokenSource();

            // Запуск фоновой отправки графика
            var chartGenerator = new ChartGenerator();
            var backgroundSender = new BackgroundSender(Bot, chartGenerator);
            var backgroundTask = backgroundSender.StartAsync(cts.Token); // 🚀 Запуск фоновой задачи

            // Запуск получения обновлений (команды, кнопки и т.д.)
            Bot.StartReceiving(
                MessageHandler.HandleUpdateAsync,
                ErrorHandler.HandleErrorAsync,
                new ReceiverOptions
                {
                    AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
                },
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот работает. Нажмите любую клавишу для остановки...");
            Console.ReadKey();

            cts.Cancel(); // Остановить бота
            await backgroundTask; // Подождать завершения фоновой задачи
        }

        public static DateTime GetBotStartTime()
        {
            return BotStartTime;
        }
    }
}
