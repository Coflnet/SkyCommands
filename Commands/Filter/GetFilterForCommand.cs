using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Core;

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
                }).Select(f => new FilterOptions(f)).ToList(), A_HOUR));
                return;
            }
            var details = await ItemDetails.Instance.GetDetailsWithCache(itemTag);
            var filters = fe.FiltersFor(details);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f)).ToList(), A_HOUR));
        }
    }
}
