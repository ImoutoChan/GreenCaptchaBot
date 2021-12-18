using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaptchaBot.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;
using Xunit.Abstractions;

namespace CaptchaBot.Tests;

public class WelcomeServiceTests
{
    private readonly UsersStore _usersStore = new UsersStore();
    private readonly Mock<ITelegramBotClient> _botMock = new Mock<ITelegramBotClient>();
    private readonly ILogger<WelcomeService> _logger;
    private readonly List<int> _deletedMessages = new();

    public WelcomeServiceTests(ITestOutputHelper outputHelper)
    {
        _logger = outputHelper.BuildLoggerFor<WelcomeService>();
        _botMock.Setup(b => b.SendTextMessageAsync(
            It.IsAny<ChatId>(),
            It.IsAny<string>(),
            It.IsAny<ParseMode>(),
            It.IsAny<IEnumerable<MessageEntity>>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<int>(),
            It.IsAny<bool>(),
            It.IsAny<IReplyMarkup>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new Message());
        _botMock.Setup(b => b.GetChatMemberAsync(
            It.IsAny<ChatId>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new ChatMember());
        _botMock.Setup(b => b.GetChatAsync(
            It.IsAny<ChatId>(),
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new Chat());
        _botMock.Setup(b => b.DeleteMessageAsync(
            It.IsAny<ChatId>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()
        )).Callback((ChatId _, int messageId, CancellationToken _) => _deletedMessages.Add(messageId));
    }

    private static Task ProcessNewChatMember(
        WelcomeService service,
        long userId,
        DateTime enterTime,
        long fromId = 0L,
        int joinMessageId = 0)
    {
        var testUser = new User {Id = userId};
        var message = new Message
        {
            MessageId = joinMessageId,
            Date = enterTime,
            Chat = new Chat(),
            From = new User {Id = fromId},
            NewChatMembers = new[]
            {
                testUser
            }
        };
        return service.ProcessNewChatMember(message);
    }

    private async Task ProcessAnswer(IWelcomeService service, bool successful)
    {
        var newUser = _usersStore.GetAll().Single();
        var callback = new CallbackQuery
        {
            Message = new Message { Chat = new Chat() },
            From = new User { Id = newUser.Id },
            Data = successful ? newUser.CorrectAnswer.ToString(CultureInfo.InvariantCulture) : "10"
        };
        await service.ProcessCallback(callback);
    }

    [Fact]
    public async Task BotShouldProcessEventWithinTimeout()
    {
        var config = new AppSettings {ProcessEventTimeout = TimeSpan.FromSeconds(5.0)};
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

        const long testUserId = 123L;
        await ProcessNewChatMember(welcomeService, testUserId, DateTime.UtcNow);

        Assert.Collection(_botMock.Invocations,
            getChatMember => Assert.Equal(nameof(ITelegramBotClient.GetChatMemberAsync), getChatMember.Method.Name),
            restrict => Assert.Equal(nameof(ITelegramBotClient.RestrictChatMemberAsync), restrict.Method.Name),
            sendMessage => Assert.Equal(nameof(ITelegramBotClient.SendTextMessageAsync), sendMessage.Method.Name));

        Assert.Equal(testUserId, _usersStore.GetAll().Single().Id);
    }

    [Fact]
    public async Task BotShouldNotProcessEventOutsideTimeout()
    {
        var config = new AppSettings {ProcessEventTimeout = TimeSpan.FromMinutes(5.0)};
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

        const int testUserId = 345;
        await ProcessNewChatMember(welcomeService, testUserId, DateTime.UtcNow - TimeSpan.FromMinutes(6.0));

        Assert.Empty(_botMock.Invocations);
        Assert.Empty(_usersStore.GetAll());
    }

    [Fact]
    public async Task BotShouldRestrictTheEnteringUserAndNotTheMessageAuthor()
    {
        var config = new AppSettings();
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

        const long enteringUserId = 123L;
        const long invitingUserId = 345L;

        await ProcessNewChatMember(welcomeService, enteringUserId, DateTime.UtcNow, invitingUserId);

        Assert.Collection(_botMock.Invocations,
            _ => {},
            restrict =>
            {
                Assert.Equal(nameof(ITelegramBotClient.RestrictChatMemberAsync), restrict.Method.Name);
                var restrictedUserId = (long)restrict.Arguments[1];
                Assert.Equal(enteringUserId, restrictedUserId);
            },
            _ => {});
    }

    [Fact]
    public async Task BotShouldNotFailIfMessageCouldNotBeDeleted()
    {
        _botMock.Setup(
                b => b.DeleteMessageAsync(It.IsAny<ChatId>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("This exception should not fail the message processing."));

        var config = new AppSettings();
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

        const long newUserId = 124L;

        await ProcessNewChatMember(welcomeService, newUserId, DateTime.UtcNow);
        await ProcessAnswer(welcomeService, true);

        Assert.Empty(_usersStore.GetAll());
    }

    private async Task DoRemoveJoinTest(
        JoinMessageDeletePolicy policy,
        int joinMessageId,
        bool successful,
        bool deleted)
    {
        const long userId = 100L;

        var config = new AppSettings { DeleteJoinMessages = policy };
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

        await ProcessNewChatMember(welcomeService, userId, DateTime.UtcNow, joinMessageId: joinMessageId);
        await ProcessAnswer(welcomeService, successful);
        if (deleted)
            Assert.Contains(joinMessageId, _deletedMessages);
        else
            Assert.DoesNotContain(joinMessageId, _deletedMessages);
    }

    [Fact]
    public Task RemoveJoinMessagesAllMode1() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.All, 123,  successful: true, deleted: true);

    [Fact]
    public Task RemoveJoinMessagesAllMode2() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.All, 124, successful: false, deleted: true);

    [Fact]
    public Task RemoveJoinMessagesNoneMode1() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.None, 321, successful: true, deleted: false);

    [Fact]
    public Task RemoveJoinMessagesNoneMode2() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.None, 421, successful: false, deleted: false);

    [Fact]
    public Task RemoveJoinMessagesUnsuccessfulMode1() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.Unsuccessful, 42, successful: true, deleted: false);

    [Fact]
    public Task RemoveJoinMessagesUnsuccessfulMode2() =>
        DoRemoveJoinTest(JoinMessageDeletePolicy.Unsuccessful, 43, successful: false, deleted: true);
}