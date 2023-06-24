using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands;

namespace SkyCommands
{
    internal class SelfHealthcheck : BackgroundService
    {
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILogger<SelfHealthcheck> _logger;

        public SelfHealthcheck(IHostApplicationLifetime appLifetime, ILogger<SelfHealthcheck> logger)
        {
            _appLifetime = appLifetime;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(30_000, stoppingToken);
                if(System.GC.GetTotalMemory(false) < 4_000_000_000)
                    continue; // "okay"

                _logger.LogInformation($"Usage of cache: {CacheService.Instance.CacheSize}");
                _logger.LogInformation($"Socket connections: {SkyblockBackEnd.ConnectionCount}");
                _logger.LogCritical("Memory usage is too high, shutting down");
                _appLifetime.StopApplication();
            }
        }
    }
}