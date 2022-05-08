using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Items.Client.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    public class GetFilterForCommand : Command
    {
        static FilterEngine fe = new FilterEngine();
        public override async Task Execute(MessageData data)
        {
            var itemTag = data.GetAs<string>();
            if (itemTag == "*")
            {
                await data.SendBack(data.Create("filterFor", fe.AvailableFilters.Where(f =>
                {
                    try
                    {
                        var options = f.Options;
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        data.LogError(e, "retrieving filter options");
                        return false;
                    }
                }).Select(f => new FilterOptions(f)).ToList(), A_HOUR * 2));
                return;
            }
            var itemApi = DiHandler.ServiceProvider.GetService<IItemsApi>();
            var item = await itemApi.ItemItemTagGetAsync(itemTag);
            var filters = fe.FiltersFor(item);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f)).ToList(), A_HOUR));
        }
    }
}
