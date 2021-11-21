using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Filter;
using Coflnet.Sky;

namespace SkyCommands
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("sky-commands");
            var FilterEngine = new FilterEngine();
            hypixel.ItemPrices.AddFilters = FilterEngine.AddFilters;
            var server = new Server();
            var itemLoad = ItemDetails.Instance.LoadLookup();
            var serverTask = Task.Run(() => server.Start()).ConfigureAwait(false);

            RunIsolatedForever(FlipperService.Instance.ListentoUnavailableTopics, "flip wait");
            RunIsolatedForever(FlipperService.Instance.ListenToNewFlips, "flip wait");
            RunIsolatedForever(FlipperService.Instance.ListenToLowPriced, "low priced auctions");
            RunIsolatedForever(FlipperService.Instance.ListenForSettingsChange, "settings sync");

            // hook up cache refreshing
            CacheService.Instance.OnCacheRefresh += Server.ExecuteCommandHeadless;

            await itemLoad;
            RunIsolatedForever(FlipperService.Instance.ProcessSlowQueue, "flip process slow", 10);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static TaskFactory factory = new TaskFactory();
        public static void RunIsolatedForever(Func<Task> todo, string message, int backoff = 2000)
        {
            factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        await todo();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"{message}: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace} {e.InnerException?.InnerException?.Message} {e.InnerException?.InnerException?.StackTrace}");
                        await Task.Delay(2000);
                    }
                    await Task.Delay(backoff);
                }
            }, TaskCreationOptions.LongRunning).ConfigureAwait(false);
        }
    }

    public static class Settings
    {
        public static bool Migrated = true;
        public static string InstanceId = "commands";
    }
}
