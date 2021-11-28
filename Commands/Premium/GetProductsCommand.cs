using System.Linq;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Newtonsoft.Json;
using Stripe;

namespace hypixel
{
    public class GetProductsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            //var service = new ProductService();
            //StripeList<Product> products = service.List(
            //  options
            //;

            var productsApi = new ProductsApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            var products = await productsApi.ProductsGetAsync();
            var topUpProducts = products.Where(p => p.Type == Coflnet.Payments.Client.Model.ProductType.NUMBER_4);

            await data.SendBack(new MessageData("productsResponse", JsonConvert.SerializeObject(topUpProducts), A_HOUR));
        }
    }
}