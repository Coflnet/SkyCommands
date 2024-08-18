using System;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using static Coflnet.Sky.Core.ItemReferences;

namespace Coflnet.Sky.Commands
{
    public class PricerDicerCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            // temporary map none (0) to any
            if (details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;


            var res = await ItemPrices.Instance.GetPriceFor(details);

            var maxAge = A_MINUTE;
            if (IsDayRange(details))
            {
                maxAge = A_DAY;
            }

            await data.SendBack(data.Create("itemResponse", res, maxAge));
        }

        private static bool IsDayRange(ItemSearchQuery details)
        {
            return details.Start < DateTime.Now - TimeSpan.FromDays(2);
        }
    }
}