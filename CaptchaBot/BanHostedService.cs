using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CaptchaBot.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace CaptchaBot;

public class BanHostedService : IHostedService
{
    private readonly ILogger<BanHostedService> _logger;
    private readonly ITelegramBotClient _telegramBot;
    private readonly IUsersStore _usersStore;
    private Timer _timer;

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
        _timer = new Timer(__ => _ = InvokeSafely(BanSlowUsers), null, 0, 10000);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
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
            await InvokeSafely(() =>
                _telegramBot.KickChatMemberAsync(newUser.ChatId, newUser.Id, DateTime.Now.AddDays(1)));
            await InvokeSafely(() => _telegramBot.DeleteMessageAsync(newUser.ChatId, newUser.InviteMessageId));
            await InvokeSafely(() => _telegramBot.DeleteMessageAsync(newUser.ChatId, newUser.JoinMessageId));
            _usersStore.Remove(newUser);

            _logger.LogInformation(
                "User {UserId} with name {UserName} was banned after one minute silence",
                newUser.Id,
                newUser.PrettyUserName);
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