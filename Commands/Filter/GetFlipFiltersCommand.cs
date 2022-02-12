using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;
using hypixel;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Special filters that are only available for the flipper as they only match <see cref="FlipInstance"/>
    /// </summary>
    public class GetFlipFiltersCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var additional = FlipFilter.AdditionalFilters.Select(f => new FilterOptions()
            {
                Name = f.Key,
                Options = f.Value.Options.Select(o => o.ToString()),
                Type = f.Value.FilterType
            }).ToList();
            return data.SendBack(data.Create("filterFor", additional, A_DAY));
        }
    }
}
