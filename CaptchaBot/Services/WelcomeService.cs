using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CaptchaBot.Services
{
    public class WelcomeService : IWelcomeService
    {
        private static readonly string[] NumberTexts =
        {
            "ноль",
            "один",
            "два",
            "три",
            "четыре",
            "пять",
            "шесть",
            "семь",
            "восемь",
        };

        private const int ButtonsCount = 8;
        private static readonly Random Random = new Random();
        private readonly AppSettings _settings;
        private readonly IUsersStore _usersStore;
        private readonly ILogger<WelcomeService> _logger;
        private readonly ITelegramBotClient _telegramBot;

        public WelcomeService(
            AppSettings settings, 
            IUsersStore usersStore, 
            ILogger<WelcomeService> logger, 
            ITelegramBotClient telegramBot)
        {
            _settings = settings;
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
                var preBanPermissions = unauthorizedUser.ChatMember;

                var postBanPermissions = new ChatPermissions
                {
                    CanAddWebPagePreviews = preBanPermissions.CanAddWebPagePreviews,
                    CanChangeInfo = preBanPermissions.CanChangeInfo,
                    CanInviteUsers = preBanPermissions.CanInviteUsers,
                    CanPinMessages = preBanPermissions.CanPinMessages,
                    CanSendMediaMessages = preBanPermissions.CanSendMediaMessages,
                    CanSendMessages = preBanPermissions.CanSendMessages,
                    CanSendOtherMessages = preBanPermissions.CanSendOtherMessages,
                    CanSendPolls = preBanPermissions.CanSendPolls
                };

                await _telegramBot.RestrictChatMemberAsync(
                    query.Message.Chat.Id,
                    query.From.Id,
                    postBanPermissions);

                _logger.LogInformation(
                    "User {UserId} with name {UserName} was authorized with answer {UserAnswer}. " +
                    "With post ban permissions {PostBanPermissions}.",
                    unauthorizedUser.Id,
                    unauthorizedUser.PrettyUserName,
                    unauthorizedUserAnswer,
                    JsonSerializer.Serialize(postBanPermissions));
            }

            await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.InviteMessageId);
            await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.JoinMessageId);
            _usersStore.Remove(unauthorizedUser);
        }

        public async Task ProcessNewChatMember(Message message)
        {
            var freshness = DateTime.UtcNow - message.Date.ToUniversalTime();
            if (freshness > _settings.ProcessEventTimeout)
            {
                _logger.LogInformation(
                    "Message about {NewChatMembers} received {Freshness} ago and ignored",
                    message.NewChatMembers.Length,
                    freshness);
                return;
            }
            
            foreach (var unauthorizedUser in message.NewChatMembers)
            {
                var answer = GetRandomNumber();

                var chatUser = await _telegramBot.GetChatMemberAsync(message.Chat.Id, unauthorizedUser.Id);

                if (chatUser == null)
                    return;

                await _telegramBot.RestrictChatMemberAsync(
                    message.Chat.Id,
                    unauthorizedUser.Id,
                    new ChatPermissions
                    {
                        CanAddWebPagePreviews = false,
                        CanChangeInfo = false,
                        CanInviteUsers = false,
                        CanPinMessages = false,
                        CanSendMediaMessages = false,
                        CanSendMessages = false,
                        CanSendOtherMessages = false,
                        CanSendPolls = false
                    },
                    DateTime.Now.AddDays(1));

                var prettyUserName = GetPrettyName(unauthorizedUser);

                var sentMessage = await _telegramBot
                    .SendTextMessageAsync(
                        message.Chat.Id, 
                        $"Привет, {prettyUserName}, нажми кнопку {GetText(answer)}, чтобы тебя не забанили!", 
                        replyToMessageId: message.MessageId, 
                        replyMarkup: new InlineKeyboardMarkup(GetKeyboardButtons()));

                _usersStore.Add(unauthorizedUser, message, sentMessage.MessageId, prettyUserName, answer, chatUser);

                
                _logger.LogInformation(
                    "The new user {UserId} with name {UserName} was detected and trialed. " +
                    "He has one minute to answer.",
                    unauthorizedUser.Id,
                    prettyUserName);
            }
        }

        private static string GetText(in int answer) => NumberTexts[answer];

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

        private static int GetRandomNumber() => Random.Next(1, ButtonsCount + 1);

        private static IEnumerable<InlineKeyboardButton> GetKeyboardButtons()
        {
            for (int i = 1; i <= ButtonsCount; i++)
            {
                var label = i.ToString();
                yield return InlineKeyboardButton.WithCallbackData(label, label);
            }
        }
    }
}