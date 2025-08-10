using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Filter;

namespace Coflnet.Sky.Commands
{
    public class GetActiveAuctionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var details = data.GetAs<ActiveItemSearchQuery>();
            var count = 20;
            if(details.Limit < count && details.Limit > 0)
                count = details.Limit;
            var engine = data.GetService<FilterEngine>();
            ItemPrices.AddFilters = engine.AddFilters;
            var service = data.GetService<ItemPrices>();
            var res = await service.GetActiveAuctions(details, count);

            await data.SendBack(data.Create("activeAuctions", res, A_MINUTE * 3));
        }
    }
}