using System;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Commands;

namespace SkyCommands
{
    public class Program
    {
        public static void Main(string[] args)
        {
            dev.Logger.Instance.Info("sky-commands");
            var FilterEngine = new FilterEngine();
            ItemPrices.AddFilters = FilterEngine.AddFilters;
            var server = new Server();
            var itemLoad = ItemDetails.Instance.LoadLookup();
            var serverTask = Task.Run(() => server.Start()).ConfigureAwait(false);

            RunIsolatedForever(FlipperService.Instance.ListentoUnavailableTopics, "flip wait");
            RunIsolatedForever(FlipperService.Instance.ListenToNewFlips, "flip wait");
            RunIsolatedForever(FlipperService.Instance.ListenToLowPriced, "low priced auctions");

            // hook up cache refreshing
            CacheService.Instance.OnCacheRefresh += Server.ExecuteCommandHeadless;
            var ignore = Task.Run(async () =>
            {
                await itemLoad;
                RunIsolatedForever(FlipperService.Instance.ProcessSlowQueue, "flip process slow", 10);
            }).ConfigureAwait(false);
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

    public static class CommandSettings
    {
        public static bool Migrated = true;
        public static string InstanceId = "commands";
    }
}
