using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Core.Services;
using Coflnet.Sky.Core;

namespace SkyCommands;

internal class WarmStart : BackgroundService
{
    private readonly ILogger<WarmStart> _logger;
    private readonly HypixelItemService hypixelItemService;
    private readonly ItemDetails itemDetails;

    public WarmStart(ILogger<WarmStart> logger, HypixelItemService hypixelItemService, ItemDetails itemDetails)
    {
        _logger = logger;
        this.hypixelItemService = hypixelItemService;
        this.itemDetails = itemDetails;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await itemDetails.LoadLookup();
            await hypixelItemService.GetItemsAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during warm start.");
        }
        finally
        {
            _logger.LogInformation("Warm start completed.");
        }
    }
}