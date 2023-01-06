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

            IServiceCollection services = new ServiceCollection();
            HttpClientHandler handler = new() { CookieContainer = new CookieContainer() { PerDomainCapacity = 50 }, AllowAutoRedirect = false };
            services.AddHttpClient("DMMGamePlayer")
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler()
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false
                    };
                });
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog(Log.Logger);
            }); ;
            IHttpClientFactory factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            ILogger<LibDMMGamePlayer> logger = services.BuildServiceProvider().GetRequiredService<ILogger<LibDMMGamePlayer>>();
            LibDMMGamePlayer libDMMGamePlayer = new(factory, logger);

            string? loginUrl = await libDMMGamePlayer.GetTokenizedLoginUrl();
            CookieContainer? cookie = await libDMMGamePlayer.UpdateCookies(loginUrl);
            string? executionArguments = await libDMMGamePlayer.GetExecutionArguments(cookie);
        }
    }
}