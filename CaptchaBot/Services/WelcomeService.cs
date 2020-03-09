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
            var unauthorizedUser = _usersStore.Get(query.Message.Chat.Id, query.From.Id);

            if (unauthorizedUser == null)
            {
                _logger.LogInformation("User with id {UserId} not found", query.From.Id);
                return;
            }

            var unauthorizedUserAnswer = int.Parse(query.Data);

            if (unauthorizedUserAnswer != unauthorizedUser.CorrectAnswer)
            {
                await _telegramBot.KickChatMemberAsync(
                    query.Message.Chat.Id,
                    query.From.Id,
                    DateTime.Now.AddDays(1));

                _logger.LogInformation(
                    "User {UserId} with name {UserName} was banned after incorrect answer {UserAnswer}, " +
                    "while correct one is {CorrectAnswer}.",
                    unauthorizedUser.Id,
                    unauthorizedUser.PrettyUserName,
                    unauthorizedUserAnswer,
                    unauthorizedUser.CorrectAnswer);
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

                _logger.LogInformation(
                    "User {UserId} with name {UserName} was authorized with answer {UserAnswer}.",
                    unauthorizedUser.Id,
                    unauthorizedUser.PrettyUserName,
                    unauthorizedUserAnswer);
            }

            await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.InviteMessageId);
            await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.JoinMessageId);
            _usersStore.Remove(unauthorizedUser);
        }

        public async Task ProcessNewChatMember(Message message)
        {
            foreach (var unauthorizedUser in message.NewChatMembers)
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

                var prettyUserName = GetPrettyName(unauthorizedUser);

                var sentMessage = await _telegramBot
                    .SendTextMessageAsync(
                        message.Chat.Id, 
                        $"Привет, {prettyUserName}, нажми кнопку {answer}, чтобы тебя не забанили!", 
                        replyToMessageId: message.MessageId, 
                        replyMarkup: new InlineKeyboardMarkup(GetKeyboardButtons()));

                _usersStore.Add(unauthorizedUser, message, sentMessage.MessageId, prettyUserName, answer);

                
                _logger.LogInformation(
                    "The new user {UserId} with name {UserName} was detected and trialed. " +
                    "He has one minute to answer.",
                    unauthorizedUser.Id,
                    prettyUserName);
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