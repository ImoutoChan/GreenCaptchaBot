using System.Text.Json;
using CaptchaBot.Services.Translation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CaptchaBot.Services;

public class WelcomeService : IWelcomeService
{
    private const int ButtonsCount = 8;
    private static readonly Random Random = new();

    private readonly AppSettings _settings;
    private readonly IUsersStore _usersStore;
    private readonly ILogger<WelcomeService> _logger;
    private readonly ITelegramBotClient _telegramBot;
    private readonly ITranslationService _translationService;

    public WelcomeService(
        AppSettings settings,
        IUsersStore usersStore,
        ILogger<WelcomeService> logger,
        ITelegramBotClient telegramBot,
        ITranslationService translationService)
    {
        _settings = settings;
        _usersStore = usersStore;
        _logger = logger;
        _telegramBot = telegramBot;
        _translationService = translationService;
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
            await _telegramBot.BanChatMember(
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

            var postBanPermissions = new ChatPermissions
            {
                CanAddWebPagePreviews = preBanPermissions.CanAddWebPagePreviews,
                CanChangeInfo = preBanPermissions.CanChangeInfo,
                CanInviteUsers = preBanPermissions.CanInviteUsers,
                CanPinMessages = preBanPermissions.CanPinMessages,
                CanSendMessages = preBanPermissions.CanSendMessages,
                CanSendOtherMessages = preBanPermissions.CanSendOtherMessages,
                CanSendPolls = preBanPermissions.CanSendPolls,
                CanManageTopics = preBanPermissions.CanManageTopics,
                CanSendAudios = preBanPermissions.CanSendAudios,
                CanSendDocuments = preBanPermissions.CanSendDocuments,
                CanSendPhotos = preBanPermissions.CanSendPhotos,
                CanSendVideos = preBanPermissions.CanSendVideos,
                CanSendVideoNotes = preBanPermissions.CanSendVideoNotes,
                CanSendVoiceNotes = preBanPermissions.CanSendVoiceNotes

            };

            await _telegramBot.RestrictChatMember(
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
            await _telegramBot.DeleteMessage(unauthorizedUser.ChatId, unauthorizedUser.InviteMessageId));

        if (_settings.DeleteJoinMessages == JoinMessageDeletePolicy.All
            || _settings.DeleteJoinMessages == JoinMessageDeletePolicy.Unsuccessful && !authorizationSuccess)
        {
            await InvokeSafely(async () =>
                await _telegramBot.DeleteMessage(unauthorizedUser.ChatId, unauthorizedUser.JoinMessageId));
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
                GetPrettyNames(message.NewChatMembers ?? []),
                freshness);
            return;
        }

        foreach (var unauthorizedUser in message.NewChatMembers ?? [])
        {
            var answer = GetRandomNumber();

            var chatUser = await _telegramBot.GetChatMember(message.Chat.Id, unauthorizedUser.Id);

            if (chatUser == null || chatUser.Status == ChatMemberStatus.Left)
                return;

            await _telegramBot.RestrictChatMember(
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
                .SendMessage(
                    message.Chat.Id,
                    _translationService.GetWelcomeMessage(prettyUserName, answer),
                    replyParameters: message.MessageId,
                    replyMarkup: new InlineKeyboardMarkup(GetKeyboardButtons()));

            _usersStore.Add(unauthorizedUser, message, sentMessage.MessageId, prettyUserName, answer, chatUser);

            _logger.LogInformation(
                "The new user {UserId} with name {UserName} was detected and trialed. " +
                "He has one minute to answer",
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
