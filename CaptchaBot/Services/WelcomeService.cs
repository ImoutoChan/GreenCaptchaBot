using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CaptchaBot.Services
{
    public class WelcomeService : IWelcomeService
    {
        private static readonly Random Random = new Random();
        private readonly IUsersStore _usersStore;
        private readonly ILogger<WelcomeService> _logger;
        private readonly ITelegramBotClient _telegramBot;

        public WelcomeService(IUsersStore usersStore, ILogger<WelcomeService> logger, ITelegramBotClient telegramBot)
        {
            _usersStore = usersStore;
            _logger = logger;
            _telegramBot = telegramBot;
        }
        
        public async Task ProcessCallback(CallbackQuery query)
        {
            var user = _usersStore.Get(query.Message.Chat.Id, query.From.Id);

            if (user == null)
            {
                _logger.LogInformation("User {UserId} not found", query.From.Id);
                return;
            }

            var userAnswer = Int32.Parse(query.Data);

            if (userAnswer != user.CorrectAnswer)
            {
                await _telegramBot.KickChatMemberAsync(
                    query.Message.Chat.Id,
                    query.From.Id,
                    DateTime.Now.AddDays(1));
            }
            else
            {
                await _telegramBot.RestrictChatMemberAsync(
                    query.Message.Chat.Id,
                    query.From.Id,
                    default,
                    true,
                    true,
                    true,
                    true);
            }

            _usersStore.Remove(user);
            await _telegramBot.DeleteMessageAsync(user.ChatId, user.InviteMessageId);
        }

        public async Task ProcessNewChatMember(Message message)
        {
            foreach (var messageNewChatMember in message.NewChatMembers)
            {
                var answer = GetRandomNumber();

                await _telegramBot.RestrictChatMemberAsync(
                    message.Chat.Id,
                    message.From.Id,
                    DateTime.Now.AddDays(1),
                    false,
                    false,
                    false,
                    false);

                var sentMessage = await _telegramBot
                    .SendTextMessageAsync(
                        message.Chat.Id, 
                        $"Привет, {GetPrettyName(messageNewChatMember)}, нажми кнопку {answer}, чтобы тебя не забанили!", 
                        replyToMessageId: message.MessageId, 
                        replyMarkup: new InlineKeyboardMarkup(GetKeyboardButtons()));

                _usersStore.Add(messageNewChatMember, message, sentMessage.MessageId, answer);
            }
        }

        private static string GetPrettyName(User messageNewChatMember)
        {
            var names = new List<string>(3);

            if (!string.IsNullOrWhiteSpace(messageNewChatMember.FirstName))
                names.Add(messageNewChatMember.FirstName);
            if (!string.IsNullOrWhiteSpace(messageNewChatMember.LastName))
                names.Add(messageNewChatMember.LastName);
            if (!string.IsNullOrWhiteSpace(messageNewChatMember.Username))
                names.Add("(@" + messageNewChatMember.Username + ")");

            return string.Join(" ", names);
        }

        private static int GetRandomNumber() => Random.Next(1, 4);

        private IEnumerable<InlineKeyboardButton> GetKeyboardButtons()
        {
            return new[]
            {
                InlineKeyboardButton.WithCallbackData("1", "1"),
                InlineKeyboardButton.WithCallbackData("2", "2"),
                InlineKeyboardButton.WithCallbackData("3", "3")
            };
        }
    }
}