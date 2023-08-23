using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Core;
using Coflnet.Sky.Items.Client.Api;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// List options for filters
    /// </summary>
    public class GetFilterOptionsCommand : Command
    {
        static FilterEngine fe = new FilterEngine();
        public override async Task Execute(MessageData data)
        {
            var optionsTask = await data.GetService<IItemsApi>().ItemItemTagModifiersAllGetAsync("*");
            var filter = fe.GetFilter(data.GetAs<string>());
            await data.SendBack(data.Create("filterOptions", new FilterOptions(filter, optionsTask), A_HOUR * 2));
        }
    }
}
