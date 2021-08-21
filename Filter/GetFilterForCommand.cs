using System.Linq;
using System.Threading.Tasks;

namespace hypixel.Filter
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
            var details = await ItemDetails.Instance.GetDetailsWithCache(itemTag);
            var filters = fe.FiltersFor(details);
            await data.SendBack(data.Create("filterFor", filters.Select(f => new FilterOptions(f)).ToList(), A_DAY));
        }
    }
}
