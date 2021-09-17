using static hypixel.ItemReferences;
using System;
using MessagePack;
using System.Threading.Tasks;

namespace hypixel
{
    public class GetActiveAuctionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var details = data.GetAs<ActiveItemSearchQuery>();
            var count = 20;
            if(details.Limit < count && details.Limit > 0)
                count = details.Limit;

            var res = await ItemPrices.Instance.GetActiveAuctions(details, count);

            await data.SendBack(data.Create("activeAuctions", res, A_MINUTE * 3));
        }
    }
}