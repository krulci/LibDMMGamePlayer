using LibDMMGamePlayer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Net;

namespace LibDMMGamePlayer.Test
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .WriteTo.Console()
                .CreateLogger();

            IHost host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddDMMGamePlayer();
                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.AddSerilog(Log.Logger);
                    });
                })
                .Build();
            IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
            ILogger<LibDMMGamePlayer> logger = host.Services.GetRequiredService<ILogger<LibDMMGamePlayer>>();
            LibDMMGamePlayer libDMMGamePlayer = new(factory, logger);

            string? loginUrl = await libDMMGamePlayer.GetTokenizedLoginUrl();
            CookieContainer? cookie = await libDMMGamePlayer.UpdateCookies(loginUrl);
            string? executionArguments = await libDMMGamePlayer.GetExecutionArguments(cookie);
        }
    }
}