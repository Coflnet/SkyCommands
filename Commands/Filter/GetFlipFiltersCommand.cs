using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Coflnet.Sky.Filter;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Special filters that are only available for the flipper as they only match <see cref="FlipInstance"/>
    /// </summary>
    public class GetFlipFiltersCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var additional = FlipFilter.AdditionalFilters.Select(f =>
            {
                try
                {
                    return new FilterOptions()
                    {
                        Name = f.Key,
                        Options = f.Value.Options.Select(o => o.ToString()),
                        Type = f.Value.FilterType
                    };
                }
                catch (System.Exception e)
                {
                    data.LogError(e, "Could not get filter options for " + f.Key);
                    return null;
                }
            }).Where(f => f != null).ToList();
            return data.SendBack(data.Create("filterFor", additional, A_DAY / 4));
        }
    }
}