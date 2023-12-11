using System.Globalization;
using CaptchaBot.Services;
using CaptchaBot.Services.Translation;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit;
using Xunit.Abstractions;

namespace CaptchaBot.Tests;

public class WelcomeServiceTests
{
    private readonly UsersStore _usersStore = new();
    private readonly ITelegramBotClient _botMock;
    private readonly ILogger<WelcomeService> _logger;
    private readonly List<int> _deletedMessages = new();
    private readonly TranslationService _translationService;

    public WelcomeServiceTests(ITestOutputHelper outputHelper)
    {
        _logger = outputHelper.BuildLoggerFor<WelcomeService>();
        _translationService = new(Options.Create<TranslationSettings>(new()));
        
        _botMock = A.Fake<ITelegramBotClient>();
        A.CallTo(() => _botMock.MakeRequestAsync(A<SendMessageRequest>._, A<CancellationToken>._))
            .Returns(new Message());

        A.CallTo(() => _botMock.MakeRequestAsync(A<GetChatMemberRequest>._, A<CancellationToken>._))
            .Returns(new ChatMemberMember());

        A.CallTo(() => _botMock.MakeRequestAsync(A<GetChatRequest>._, A<CancellationToken>._))
            .Returns(new Chat());

        A.CallTo(() => _botMock.MakeRequestAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .Invokes((IRequest<bool> request, CancellationToken _) => _deletedMessages.Add((request as DeleteMessageRequest)!.MessageId));
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
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock, _translationService);

        const long testUserId = 123L;
        await ProcessNewChatMember(welcomeService, testUserId, DateTime.UtcNow);

        Assert.Collection(Fake.GetCalls(_botMock),
            getChatMember =>
            {
                Assert.Equal(nameof(ITelegramBotClient.MakeRequestAsync), getChatMember.Method.Name);
                Assert.IsType<GetChatMemberRequest>(getChatMember.Arguments.First());
            },
            restrict =>
            {
                Assert.Equal(nameof(ITelegramBotClient.MakeRequestAsync), restrict.Method.Name);
                Assert.IsType<RestrictChatMemberRequest>(restrict.Arguments.First());
            },
            sendMessage =>
            {
                Assert.Equal(nameof(ITelegramBotClient.MakeRequestAsync), sendMessage.Method.Name);
                Assert.IsType<SendMessageRequest>(sendMessage.Arguments.First());
            });

        Assert.Equal(testUserId, _usersStore.GetAll().Single().Id);
    }

    [Fact]
    public async Task BotShouldNotProcessEventOutsideTimeout()
    {
        var config = new AppSettings {ProcessEventTimeout = TimeSpan.FromMinutes(5.0)};
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock, _translationService);

        const int testUserId = 345;
        await ProcessNewChatMember(welcomeService, testUserId, DateTime.UtcNow - TimeSpan.FromMinutes(6.0));

        Assert.Empty(Fake.GetCalls(_botMock));
        Assert.Empty(_usersStore.GetAll());
    }

    [Fact]
    public async Task BotShouldRestrictTheEnteringUserAndNotTheMessageAuthor()
    {
        var config = new AppSettings();
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock, _translationService);

        const long enteringUserId = 123L;
        const long invitingUserId = 345L;

        await ProcessNewChatMember(welcomeService, enteringUserId, DateTime.UtcNow, invitingUserId);

        Assert.Collection(Fake.GetCalls(_botMock),
            _ => {},
            restrict =>
            {
                Assert.Equal(nameof(ITelegramBotClient.MakeRequestAsync), restrict.Method.Name);
                var restrictedUserId = (RestrictChatMemberRequest)restrict.Arguments[0]!;
                Assert.Equal(enteringUserId, restrictedUserId.UserId);
            },
            _ => {});
    }

    [Fact]
    public async Task BotShouldNotFailIfMessageCouldNotBeDeleted()
    {
        A.CallTo(() => _botMock.MakeRequestAsync(A<DeleteMessageRequest>._, A<CancellationToken>._))
            .Throws(new Exception("This exception should not fail the message processing."));

        var config = new AppSettings();
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock, _translationService);

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
        var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock, _translationService);

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
