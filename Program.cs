global using RestSharp;
global using System;
global using System.Collections.Generic;
global using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Hosting;
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
            var itemLoad = ItemDetails.Instance.LoadLookup();
            var serverTask = Task.Run(() => server.Start()).ConfigureAwait(false);
            // increase threadpool size
            System.Threading.ThreadPool.SetMinThreads(100, 100);

            // hook up cache refreshing
            CacheService.Instance.OnCacheRefresh += Server.ExecuteCommandHeadless;

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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
