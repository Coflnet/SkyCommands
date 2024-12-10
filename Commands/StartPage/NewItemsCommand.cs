using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands;
public class NewItemsCommand : Command
{
    public override async Task Execute(MessageData data)
    {
        var items = await Shared.DiHandler.ServiceProvider.GetService<Sky.Items.Client.Api.IItemsApi>()
            .ItemsItemsAhNewGetAsync();
        await data.SendBack(data.Create("newItemsResponse", items, A_HOUR));
    }
}
