using CaptchaBot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Telegram.Bot;

namespace CaptchaBot
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            services.AddOptions();
            services.Configure<AppSettings>(Configuration.GetSection("Configuration"));

            services.AddTransient(ser => ser.GetService<IOptions<AppSettings>>().Value);

            services.AddSingleton<ITelegramBotClient>(
                x =>
                {
                    var settings = x.GetRequiredService<AppSettings>();
                    return new TelegramBotClient(settings.BotToken);
                });

            services.AddSingleton<IUsersStore, UsersStore>();
            services.AddTransient<IWelcomeService, WelcomeService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseTelegramBotWebhook();

            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }

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

    public class AppSettings
    {
        public string BotToken { get; set; }
        public string WebHookAddress { get; set; }
    }
}