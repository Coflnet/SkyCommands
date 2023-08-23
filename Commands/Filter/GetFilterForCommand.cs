using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Core;
using Coflnet.Sky.Items.Client.Api;

namespace Coflnet.Sky.Commands
{
    public class GetFilterForCommand : Command
    {
        static FilterEngine fe = new FilterEngine();
        public override async Task Execute(MessageData data)
        {
            var itemTag = data.GetAs<string>();
            var itemApi = data.GetService<IItemsApi>();
            var optionsTask = itemApi.ItemItemTagModifiersAllGetAsync("*");
            if (itemTag == "*")
            {
                var all = await optionsTask;
                await data.SendBack(data.Create("filterFor", fe.AvailableFilters.Where(f =>
                {
                    try
                    {
                        var options = f.OptionsGet(new OptionValues(all));
                        return true;
                    }
                    catch (System.Exception e)
                    {
                        data.LogError(e, "retrieving filter options");
                        return false;
                    }
                }).Select(f => new FilterOptions(f, all)).ToList(), A_HOUR * 2));
                return;
            }
            var item = await itemApi.ItemItemTagGetAsync(itemTag);
            var allOptions = await optionsTask;
            var filters = fe.FiltersFor(item);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f,allOptions)).ToList(), A_HOUR));
        }
    }
}
