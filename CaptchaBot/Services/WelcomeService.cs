using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CaptchaBot.Services;

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
        var chatId = query.Message!.Chat.Id;
        var unauthorizedUser = _usersStore.Get(chatId, query.From.Id);
        bool authorizationSuccess;

        if (unauthorizedUser == null)
        {
            _logger.LogInformation("User with id {UserId} not found", query.From.Id);
            return;
        }

        var unauthorizedUserAnswer = int.Parse(query.Data!);

        if (unauthorizedUserAnswer != unauthorizedUser.CorrectAnswer)
        {
            await _telegramBot.BanChatMemberAsync(
                chatId,
                query.From.Id,
                DateTime.Now.AddDays(1));
            authorizationSuccess = false;

            _logger.LogInformation(
                "User {UserId} with name {UserName} was banned after incorrect answer {UserAnswer}, " +
                "while correct one is {CorrectAnswer}",
                unauthorizedUser.Id,
                unauthorizedUser.PrettyUserName,
                unauthorizedUserAnswer,
                unauthorizedUser.CorrectAnswer);
        }
        else
        {
            var preBanPermissions = GetPreBanPermissions(unauthorizedUser.ChatMember);

            var defaultPermissions = (await _telegramBot.GetChatAsync(chatId)).Permissions;

            var postBanPermissions = new ChatPermissions
            {
                CanAddWebPagePreviews = preBanPermissions.CanAddWebPagePreviews ?? defaultPermissions?.CanAddWebPagePreviews ?? true,
                CanChangeInfo = preBanPermissions.CanChangeInfo ?? defaultPermissions?.CanChangeInfo ?? true,
                CanInviteUsers = preBanPermissions.CanInviteUsers ?? defaultPermissions?.CanInviteUsers ?? true,
                CanPinMessages = preBanPermissions.CanPinMessages ?? defaultPermissions?.CanPinMessages ?? true,
                CanSendMessages = preBanPermissions.CanSendMessages ?? defaultPermissions?.CanSendMessages ?? true,
                CanSendOtherMessages = preBanPermissions.CanSendOtherMessages ?? defaultPermissions?.CanSendOtherMessages ?? true,
                CanSendPolls = preBanPermissions.CanSendPolls ?? defaultPermissions?.CanSendPolls ?? true,
                CanManageTopics = preBanPermissions.CanManageTopics ?? defaultPermissions?.CanManageTopics ?? true,
                CanSendAudios = preBanPermissions.CanSendAudios ?? defaultPermissions?.CanSendAudios ?? true,
                CanSendDocuments = preBanPermissions.CanSendDocuments ?? defaultPermissions?.CanSendDocuments ?? true,
                CanSendPhotos = preBanPermissions.CanSendPhotos ?? defaultPermissions?.CanSendPhotos ?? true,
                CanSendVideos = preBanPermissions.CanSendVideos ?? defaultPermissions?.CanSendVideos ?? true,
                CanSendVideoNotes = preBanPermissions.CanSendVideoNotes ?? defaultPermissions?.CanSendVideoNotes ?? true,
                CanSendVoiceNotes = preBanPermissions.CanSendVoiceNotes ?? defaultPermissions?.CanSendVoiceNotes ?? true,

            };

            await _telegramBot.RestrictChatMemberAsync(
                chatId,
                query.From.Id,
                postBanPermissions);
            authorizationSuccess = true;

            _logger.LogInformation(
                "User {UserId} with name {UserName} was authorized with answer {UserAnswer}. " +
                "With post ban permissions {PostBanPermissions}",
                unauthorizedUser.Id,
                unauthorizedUser.PrettyUserName,
                unauthorizedUserAnswer,
                JsonSerializer.Serialize(postBanPermissions));
        }

        await InvokeSafely(async () =>
            await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.InviteMessageId));

        if (_settings.DeleteJoinMessages == JoinMessageDeletePolicy.All
            || _settings.DeleteJoinMessages == JoinMessageDeletePolicy.Unsuccessful && !authorizationSuccess)
        {
            await InvokeSafely(async () =>
                await _telegramBot.DeleteMessageAsync(unauthorizedUser.ChatId, unauthorizedUser.JoinMessageId));
        }

        _usersStore.Remove(unauthorizedUser);
    }

    private ChatPermissions GetPreBanPermissions(ChatMember chatMember)
    {
        return chatMember switch
        {
            ChatMemberAdministrator chatMemberAdministrator => new ChatPermissions
            {
                CanSendMessages = true,
                CanSendAudios = true,
                CanSendDocuments = true,
                CanSendPhotos = true,
                CanSendVideos = true,
                CanSendVideoNotes = true,
                CanSendVoiceNotes = true,
                CanSendPolls = true,
                CanSendOtherMessages = true,
                CanAddWebPagePreviews = true,
                CanManageTopics = chatMemberAdministrator.CanManageTopics,
                CanChangeInfo = chatMemberAdministrator.CanChangeInfo,
                CanInviteUsers = chatMemberAdministrator.CanInviteUsers,
                CanPinMessages = chatMemberAdministrator.CanPinMessages
            },
            ChatMemberBanned _ => new ChatPermissions(),
            ChatMemberLeft _ => new ChatPermissions(),
            ChatMemberMember _ => new ChatPermissions(),
            ChatMemberOwner _ => new ChatPermissions
            {
                CanSendMessages = true,
                CanManageTopics = true,
                CanSendAudios = true,
                CanSendDocuments = true,
                CanSendPhotos = true,
                CanSendVideos = true,
                CanSendVideoNotes = true,
                CanSendVoiceNotes = true,
                CanSendPolls = true,
                CanSendOtherMessages = true,
                CanAddWebPagePreviews = true,
                CanChangeInfo = true,
                CanInviteUsers = true,
                CanPinMessages = true
            },
            ChatMemberRestricted chatMemberRestricted => new ChatPermissions
            {
                CanSendMessages = chatMemberRestricted.CanSendMessages,
                CanManageTopics = chatMemberRestricted.CanManageTopics,
                CanSendAudios = chatMemberRestricted.CanSendAudios,
                CanSendDocuments = chatMemberRestricted.CanSendDocuments,
                CanSendPhotos = chatMemberRestricted.CanSendPhotos,
                CanSendVideos = chatMemberRestricted.CanSendVideos,
                CanSendVideoNotes = chatMemberRestricted.CanSendVideoNotes,
                CanSendVoiceNotes = chatMemberRestricted.CanSendVoiceNotes,
                CanSendPolls = chatMemberRestricted.CanSendPolls,
                CanSendOtherMessages = chatMemberRestricted.CanSendOtherMessages,
                CanAddWebPagePreviews = chatMemberRestricted.CanAddWebPagePreviews,
                CanChangeInfo = chatMemberRestricted.CanChangeInfo,
                CanInviteUsers = chatMemberRestricted.CanInviteUsers,
                CanPinMessages = chatMemberRestricted.CanPinMessages
            },
            _ => new ChatPermissions()
        };
    }

    public async Task ProcessNewChatMember(Message message)
    {
        var freshness = DateTime.UtcNow - message.Date.ToUniversalTime();
        if (freshness > _settings.ProcessEventTimeout)
        {
            _logger.LogWarning(
                "Message about {NewChatMembers} received {Freshness} ago and ignored",
                GetPrettyNames(message.NewChatMembers ?? Array.Empty<User>()),
                freshness);
            return;
        }

        foreach (var unauthorizedUser in message.NewChatMembers ?? Array.Empty<User>())
        {
            var answer = GetRandomNumber();

            var chatUser = await _telegramBot.GetChatMemberAsync(message.Chat.Id, unauthorizedUser.Id);

            if (chatUser == null || chatUser.Status == ChatMemberStatus.Left)
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
                    CanManageTopics = false,
                    CanSendAudios = false,
                    CanSendDocuments = false,
                    CanSendPhotos = false,
                    CanSendVideos = false,
                    CanSendVideoNotes = false,
                    CanSendVoiceNotes = false,
                    CanSendMessages = false,
                    CanSendOtherMessages = false,
                    CanSendPolls = false
                },
                true,
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
                "He has one minute to answer",
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

    private static string GetPrettyNames(IEnumerable<User> users) => string.Join(", ", users.Select(GetPrettyName));

    private static int GetRandomNumber() => Random.Next(1, ButtonsCount + 1);

    private static IEnumerable<InlineKeyboardButton> GetKeyboardButtons()
    {
        for (int i = 1; i <= ButtonsCount; i++)
        {
            var label = i.ToString();
            yield return InlineKeyboardButton.WithCallbackData(label, label);
        }
    }

    private async Task InvokeSafely(Func<Task> func)
    {
        try
        {
            await func();
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "An error occured");
        }
    }
}
