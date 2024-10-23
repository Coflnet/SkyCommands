using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Core.Services;

namespace SkyCommands;
internal class WarmStart : BackgroundService
{
    private readonly ILogger<WarmStart> _logger;
    private readonly HypixelItemService hypixelItemService;

    public WarmStart(ILogger<WarmStart> logger, HypixelItemService hypixelItemService)
    {
        _logger = logger;
        this.hypixelItemService = hypixelItemService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await hypixelItemService.GetItemsAsync();
    }
}