using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;
using hypixel;

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
                await data.SendBack(data.Create("filterFor", fe.AvailableFilters.Select(f => new FilterOptions(f)).ToList(), A_DAY));
                return;
            }
            if (itemTag == "flips")
            {
                var additional = FlipFilter.AdditionalFilters.Select(f=> new FilterOptions()
                {
                    Name = f.Key,
                    Options = f.Value.Options.Select(o=>o.ToString())
                });
                var options = fe.AvailableFilters.Select(f => new FilterOptions(f)).Union(additional).ToList();
                await data.SendBack(data.Create("filterFor", options, A_DAY));
                return;
            }
            var details = await ItemDetails.Instance.GetDetailsWithCache(itemTag);
            var filters = fe.FiltersFor(details);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f)).ToList(), A_DAY));
        }
    }
}
