using Newtonsoft.Json;
using Telegram.Bot;

namespace CaptchaBot;

public static class StartupExtensions
{
    public static IApplicationBuilder UseTelegramBotWebhook(this IApplicationBuilder applicationBuilder)
    {
        var services = applicationBuilder.ApplicationServices;

        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStarted.Register(
            () =>
            {
                var logger = services.GetRequiredService<ILogger<Startup>>();
                var address = services.GetRequiredService<AppSettings>().WebHookAddress;

                async Task ResetWebHook()
                {
                    logger.LogInformation("Removing webhook");
                    await services.GetRequiredService<ITelegramBotClient>().DeleteWebhook();

                    logger.LogInformation($"Setting webhook to {address}");
                    await services.GetRequiredService<ITelegramBotClient>().SetWebhook(address, maxConnections: 5);
                    logger.LogInformation($"Webhook is set to {address}");

                    var webhookInfo = await services.GetRequiredService<ITelegramBotClient>().GetWebhookInfo();
                    logger.LogInformation($"Webhook info: {JsonConvert.SerializeObject(webhookInfo)}");
                }

                _ = ResetWebHook();
            });

        lifetime.ApplicationStopping.Register(
            () =>
            {
                var logger = services.GetRequiredService<ILogger<Startup>>();

                services.GetRequiredService<ITelegramBotClient>().DeleteWebhook().Wait();
                logger.LogInformation("Webhook removed");
            });

        return applicationBuilder;
    }
}
