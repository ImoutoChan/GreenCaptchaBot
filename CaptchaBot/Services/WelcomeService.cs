using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System.Linq;

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

        private static int ButtonsCount = NumberTexts.Length;
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
            var chatId = query.Message.Chat.Id;
            var unauthorizedUser = _usersStore.Get(chatId, query.From.Id);

            if (unauthorizedUser == null)
            {
                _logger.LogInformation("User with id {UserId} not found", query.From.Id);
                return;
            }


            if (int.TryParse(query.Data, out int result))
            {
                this._logger.LogInformation($"The number is not valid:{query.Data}");
                return;
            }
            var unauthorizedUserAnswer = result;

            if (unauthorizedUserAnswer != unauthorizedUser.CorrectAnswer)
            {
                string log = await KickChatAsync(chatId, query.From.Id, unauthorizedUser);
                if (log != null) this._logger.LogInformation($"KickChatAsync:{log}");
            }
            else
            {
                ChatMember preBanPermissions = unauthorizedUser.ChatMember;

                ChatPermissions defaultPermissions = (await _telegramBot.GetChatAsync(chatId)).Permissions;

                var postBanPermissions = CreateChatPermissions(preBanPermissions, defaultPermissions);//

                await _telegramBot.RestrictChatMemberAsync(
                    chatId,
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
        private ChatPermissions CreateChatPermissions(ChatMember? preBanPermissions, ChatPermissions? defaultPermissions)
        {
            var postBanPermissions = new ChatPermissions
            {
                CanAddWebPagePreviews = Check(preBanPermissions.CanAddWebPagePreviews, defaultPermissions?.CanAddWebPagePreviews),
                CanChangeInfo = Check(preBanPermissions.CanChangeInfo, defaultPermissions?.CanChangeInfo),
                CanInviteUsers = Check(preBanPermissions.CanInviteUsers, defaultPermissions?.CanInviteUsers),
                CanPinMessages = Check(preBanPermissions.CanPinMessages, defaultPermissions?.CanPinMessages),
                CanSendMediaMessages = Check(preBanPermissions.CanSendMediaMessages, defaultPermissions?.CanSendMediaMessages),
                CanSendMessages = Check(preBanPermissions.CanSendMessages, defaultPermissions?.CanSendMessages),
                CanSendOtherMessages = Check(preBanPermissions.CanSendOtherMessages, defaultPermissions?.CanSendOtherMessages),
                CanSendPolls = Check(preBanPermissions.CanSendPolls, defaultPermissions?.CanSendPolls)
            };
            return postBanPermissions;
        }
        private bool Check(bool? one, bool? two)
        {
            if (one != null) return one.Value;
            if (one != null) return two.Value;
            return true;
        }
        private async Task<string> KickChatAsync(long chatId, int queryFromId, NewUser? newUser)
        {
            try
            {
                await _telegramBot.KickChatMemberAsync(
                        chatId,
                        queryFromId,
                        DateTime.Now.AddDays(1));

                _logger.LogInformation(
                    "User {UserId} with name {UserName} was banned after incorrect answer {UserAnswer}, " +
                    "while correct one is {CorrectAnswer}.",
                    newUser.Id,
                    newUser.PrettyUserName,
                    newUser,
                    newUser.CorrectAnswer);
            }
            catch (Exception ex) { return ex.Message; };
            return null;
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
            var array = new string[] { messageNewChatMember.FirstName ,
                                       messageNewChatMember.LastName,
                                       messageNewChatMember.Username 
            };
            return string.Join(" ", array.Where(q => !string.IsNullOrWhiteSpace(q)));
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