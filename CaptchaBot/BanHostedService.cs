using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaptchaBot.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace CaptchaBot
{
    public class BanHostedService : IHostedService
    {
        private Timer _timer;
        private readonly IUsersStore _usersStore;
        private readonly ILogger<BanHostedService> _logger;
        private readonly ITelegramBotClient _telegramBot;

        public BanHostedService(
            IUsersStore usersStore, 
            ILogger<BanHostedService> logger, 
            ITelegramBotClient telegramBot)
        {
            _usersStore = usersStore;
            _logger = logger;
            _telegramBot = telegramBot;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(async x => await BanSlowUsers(), null, 0, 10000);
            return Task.CompletedTask;
        }

        private async Task BanSlowUsers()
        {
            var users = _usersStore.GetAll();

            var usersToBan = users.Where(
                    x =>
                    {
                        var diff = DateTimeOffset.Now - x.JoinDateTime;
                        return diff > TimeSpan.FromSeconds(60);
                    })
                .ToArray();

            foreach (var newUser in usersToBan)
            {
                await _telegramBot.KickChatMemberAsync(newUser.ChatId, newUser.Id, DateTime.Now.AddDays(1));
                await _telegramBot.DeleteMessageAsync(newUser.ChatId, newUser.InviteMessageId);
                _usersStore.Remove(newUser);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //New Timer does not have a stop. 
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
    }
}