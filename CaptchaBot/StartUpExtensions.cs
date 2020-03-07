using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;

namespace CaptchaBot
{
    public static class StartUpExtensions
    {
        public static IApplicationBuilder UseTelegramBotWebhook(this IApplicationBuilder applicationBuilder)
        {
            var services = applicationBuilder.ApplicationServices;

            var lifetime = services.GetService<IHostApplicationLifetime>();

            lifetime.ApplicationStarted.Register(
                async () =>
                {
                    var logger = services.GetRequiredService<ILogger<Startup>>();
                    var address = services.GetRequiredService<AppSettings>().WebHookAddress;

                    logger.LogInformation("Removing webhook");
                    await services.GetService<ITelegramBotClient>().DeleteWebhookAsync();

                    logger.LogInformation($"Setting webhook to {address}");
                    await services.GetService<ITelegramBotClient>().SetWebhookAsync(address, maxConnections: 5);
                    logger.LogInformation($"Webhook is set to {address}");

                    var webhookInfo = await services.GetService<ITelegramBotClient>().GetWebhookInfoAsync();
                    logger.LogInformation($"Webhook info: {JsonConvert.SerializeObject(webhookInfo)}");
                });

            lifetime.ApplicationStopping.Register(
                () =>
                {
                    var logger = services.GetService<ILogger<Startup>>();

                    services.GetService<ITelegramBotClient>().DeleteWebhookAsync().Wait();
                    logger.LogInformation("Webhook removed");
                });

            return applicationBuilder;
        }
    }
}