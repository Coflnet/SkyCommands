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

namespace SkyCommands
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("sky-commands");
            var FilterEngine = new FilterEngine();
            hypixel.ItemPrices.AddFilters = FilterEngine.AddFilters;
            var server = new Server();
            Task.Run(() => server.Start()).ConfigureAwait(false);
            ItemDetails.Instance.LoadLookup();

            hypixel.Program.RunIsolatedForever(FlipperService.Instance.ListentoUnavailableTopics, "flip wait");
            hypixel.Program.RunIsolatedForever(FlipperService.Instance.ListenToNewFlips, "flip wait");
            hypixel.Program.RunIsolatedForever(FlipperService.Instance.ProcessSlowQueue, "flip process slow", 10);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }

    public static class Settings
    {
        public static bool Migrated = true;
        public static string InstanceId = "skycommands";
    }
}
