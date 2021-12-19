using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CaptchaBot;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(
                (_, config)
                    => config.AddJsonFile("Configuration/appsettings.json", optional: false, reloadOnChange: true))
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
            .ConfigureServices(services => services.AddHostedService<BanHostedService>())
            .ConfigureSerilog(
                (logger, _)
                    => logger
                        .WithoutDefaultLoggers()
                        .WithConsole()
                        .WithAllRollingFile()
                        .WithInformationRollingFile());
}