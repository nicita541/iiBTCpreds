using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace ConsoleApp13
{
    internal class MessageHandler
    {
        private static string ChatIdFilePath = "chat_ids.txt";

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is { } message && message.Text is { })
            {
                var botStartTime = Program.GetBotStartTime();
                if (message.Date.ToUniversalTime() < botStartTime)
                    return;

                var chatId = message.Chat.Id;
                var messageText = message.Text;

                Console.WriteLine($"Новое сообщение от {chatId}: {messageText}");

                switch (messageText)
                {
                    case "/start":
                       
                        var keyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Bitcoin", "Bitcoin") }
                        });

                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Какую валюту хотите отслеживать ?",
                            replyMarkup: keyboard,
                            cancellationToken: cancellationToken
                        );
                        break;

                    case "/stop":
                        RemoveChatId(chatId);

                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Вы отписались от уведомлений!",
                            cancellationToken: cancellationToken
                        );
                        break;

                    default:
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"Я не знаю такой команды: {messageText}",
                            cancellationToken: cancellationToken
                        );
                        break;
                }
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleButtonPress(botClient, callbackQuery, cancellationToken);
            }
        }

        private static async Task HandleButtonPress(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;

            // нажал на кнопку биток
            if (callbackQuery.Data == "Bitcoin")
            {
                SaveChatId(chatId);
                // Считаем время до следующей рассылки
                TimeSpan timeUntilNext = GetTimeUntilNextRun();

                string timeMessage = $"Вы подписались на получение уведомлений!\n" +
                                     $"⏰ Следующая рассылка будет через {timeUntilNext.Minutes} мин {timeUntilNext.Seconds} сек.";

                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: timeMessage,
                    cancellationToken: cancellationToken
                );

            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
        private static TimeSpan GetTimeUntilNextRun()
        {
            var now = DateTime.Now;
            int minutes = now.Minute;
            int nextMinutes = ((minutes / 25) + 1) * 25;

            int addHours = 0;
            if (nextMinutes >= 60)
            {
                nextMinutes = 0;
                addHours = 1;
            }

            var nextRun = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0)
                .AddHours(addHours)
                .AddMinutes(nextMinutes);

            return nextRun - now;
        }

        private static void SaveChatId(long chatId)
        {
            var existingIds = File.Exists(ChatIdFilePath) ? File.ReadAllLines(ChatIdFilePath) : Array.Empty<string>();

            if (!existingIds.Contains(chatId.ToString()))
            {
                File.AppendAllText(ChatIdFilePath, chatId + Environment.NewLine);
                Console.WriteLine($"chat_id {chatId} сохранён.");
            }
        }

        private static void RemoveChatId(long chatId)
        {
            if (File.Exists(ChatIdFilePath))
            {
                var existingIds = File.ReadAllLines(ChatIdFilePath).ToList();
                if (existingIds.Remove(chatId.ToString()))
                {
                    File.WriteAllLines(ChatIdFilePath, existingIds);
                    Console.WriteLine($"chat_id {chatId} удалён.");
                }
            }
        }
    }
}
