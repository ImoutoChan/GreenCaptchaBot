using CaptchaBot.Services;
using CaptchaBot.Services.Translation;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace CaptchaBot;

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
        services.AddControllers();
        services.ConfigureTelegramBotMvc();

        services.AddOptions();
        services.Configure<AppSettings>(Configuration.GetSection("Configuration"));
        services.Configure<TranslationSettings>(Configuration.GetSection("Translation"));

        services.AddTransient(ser => ser.GetRequiredService<IOptions<AppSettings>>().Value);

        services.AddSingleton<ITelegramBotClient>(
            x =>
            {
                var settings = x.GetRequiredService<AppSettings>();
                return new TelegramBotClient(settings.BotToken);
            });

        services.AddSingleton<IUsersStore, UsersStore>();
        services.AddTransient<IWelcomeService, WelcomeService>();
        services.AddTransient<ITranslationService, TranslationService>();
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
