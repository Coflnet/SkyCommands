using Coflnet.Security.OpenBao;
global using RestSharp;
global using System;
global using System.Collections.Generic;
global using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Commands;

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
