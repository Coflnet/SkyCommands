using System.Threading.Tasks;
using Newtonsoft.Json;
using Stripe;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetPricesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            throw new CoflnetException("deactive","We do currently not sell premium. Check back tomorrow :)");
            var options = new PriceListOptions { Limit = 10 };
            var service = new PriceService();
            StripeList<Price> prices = service.List(options);

            return data.SendBack(new MessageData("pricesResponse", JsonConvert.SerializeObject(prices), A_HOUR));
        }
    }
}