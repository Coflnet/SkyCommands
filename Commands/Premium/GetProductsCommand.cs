using System.Linq;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Newtonsoft.Json;
using Stripe;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetProductsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var productsApi = new ProductsApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            var topUpProducts = await productsApi.ProductsTopupGetAsync(0,100);

            await data.SendBack(new MessageData("productsResponse", JsonConvert.SerializeObject(topUpProducts), A_HOUR));
        }
    }
}