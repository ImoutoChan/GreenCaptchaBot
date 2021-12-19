#nullable enable
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CaptchaBot;

public static class SerilogExtensions
{
    private const string FileTemplate 
        = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] <s:{SourceContext}> {Message}{NewLine}{Exception}";
    private const string ConsoleTemplate 
        = "[{Timestamp:HH:mm:ss} {Level:u3}] <s:{SourceContext}> {Message:lj}{NewLine}{Exception}";
        

    public static LoggerConfiguration WithoutDefaultLoggers(this LoggerConfiguration configuration)
        => configuration.MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning);

    public static LoggerConfiguration WithConsole(this LoggerConfiguration configuration)
        => configuration.WriteTo.Console(
            outputTemplate: ConsoleTemplate,
            restrictedToMinimumLevel: LogEventLevel.Verbose);

    public static LoggerConfiguration WithAllRollingFile(
        this LoggerConfiguration configuration,
        string pathFormat = "logs/{Date}-all.log")
        => configuration.WriteTo.RollingFile(
            pathFormat: pathFormat,
            outputTemplate: FileTemplate,
            restrictedToMinimumLevel: LogEventLevel.Verbose);

    public static LoggerConfiguration WithInformationRollingFile(
        this LoggerConfiguration configuration,
        string pathFormat = "logs/{Date}-information.log")
        => configuration.WriteTo.RollingFile(
            pathFormat: pathFormat,
            outputTemplate: FileTemplate,
            restrictedToMinimumLevel: LogEventLevel.Information);

    public static LoggerConfiguration PatchWithConfiguration(
        this LoggerConfiguration configuration,
        IConfiguration appConfiguration)
        => configuration.ReadFrom.Configuration(appConfiguration);
}

public static class HostBuilderExtensions
{
    public static IHostBuilder ConfigureSerilog(
        this IHostBuilder hostBuilder,
        Action<LoggerConfiguration, IConfiguration>? configureLogger = null)
    {
        hostBuilder.ConfigureLogging((context, builder) =>
        {
            builder.ClearProviders();
            builder.AddSerilog(
                dispose: true, 
                logger: GetSerilogLogger(context.Configuration, configureLogger));
        });

        return hostBuilder;
    }

    private static Logger GetSerilogLogger(
        IConfiguration configuration,
        Action<LoggerConfiguration, IConfiguration>? configureLogger)
    {
        var loggerBuilder = new LoggerConfiguration()
            .Enrich.FromLogContext();

        configureLogger?.Invoke(loggerBuilder, configuration);

        return loggerBuilder.CreateLogger();
    }
}