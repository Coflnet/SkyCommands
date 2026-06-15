global using RestSharp;
global using System;
global using System.Collections.Generic;
global using System.Linq;
using Coflnet.Core;
using Coflnet.Security.OpenBao;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands;
using Coflnet.Sky.Filter;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace SkyCommands
{
    public class Program
    {
        public static void Main(string[] args)
        {
            dev.Logger.Instance.Info("sky-commands");
            var server = new Server();
            var serverTask = Task.Run(() => server.Start()).ConfigureAwait(false);
            System.Threading.ThreadPool.SetMinThreads(100, 100);

            CacheService.Instance.OnCacheRefresh += Server.ExecuteCommandHeadless;

            var host = CreateHostBuilder(args).Build();
            HypixelContext.SetConfiguration(host.Services.GetRequiredService<IConfiguration>());
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((_, config) => config.AddOpenBaoFromEnvironment())
                .ConfigureLogging((context, logging) =>
                {
                    // Shared OTel logging configuration from Coflnet.Core.
                    // Bridges ILogger -> OTLP (HttpProtobuf) so logs land in Loki, correlated with traces in Jaeger.
                    logging.AddOpenTelemetryLogging(
                        context.Configuration,
                        context.Configuration["JAEGER_SERVICE_NAME"] ?? "sky-commands");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }

    public static class CommandSettings
    {
        public static bool Migrated = true;
        public static string InstanceId = "commands";
    }
}
