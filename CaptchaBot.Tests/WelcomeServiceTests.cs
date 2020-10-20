using System;
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

namespace CaptchaBot.Tests
{
    public class WelcomeServiceTests
    {
        private readonly UsersStore _usersStore = new UsersStore();
        private readonly Mock<ITelegramBotClient> _botMock = new Mock<ITelegramBotClient>();
        private readonly ILogger<WelcomeService> _logger;

        public WelcomeServiceTests(ITestOutputHelper outputHelper)
        {
            _logger = outputHelper.BuildLoggerFor<WelcomeService>();
            _botMock.Setup(b => b.SendTextMessageAsync(
                It.IsAny<ChatId>(),
                It.IsAny<string>(),
                It.IsAny<ParseMode>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<IReplyMarkup>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new Message());
        }

        private static Task ProcessNewChatMember(WelcomeService service, int userId, DateTime enterTime, int fromId = 0)
        {
            var testUser = new User {Id = userId}; 
            var message = new Message
            {
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

        [Fact]
        public async Task BotShouldProcessEventWithinTimeout()
        {
            var config = new AppSettings {ProcessEventTimeout = TimeSpan.FromSeconds(5.0)};
            var welcomeService = new WelcomeService(config, _usersStore, _logger, _botMock.Object);

            const int testUserId = 123;
            await ProcessNewChatMember(welcomeService, testUserId, DateTime.UtcNow);

            Assert.Collection(_botMock.Invocations, 
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

            const int enteringUserId = 123;
            const int invitingUserId = 345;
            
            await ProcessNewChatMember(welcomeService, enteringUserId, DateTime.UtcNow, invitingUserId);
            
            Assert.Collection(_botMock.Invocations,
                restrict =>
                {
                    Assert.Equal(nameof(ITelegramBotClient.RestrictChatMemberAsync), restrict.Method.Name);
                    var restrictedUserId = (int)restrict.Arguments[1];
                    Assert.Equal(enteringUserId, restrictedUserId);
                },
                _ => {});
        }
    }
}