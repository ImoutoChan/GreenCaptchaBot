using CaptchaBot.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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