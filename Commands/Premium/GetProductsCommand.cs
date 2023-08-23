using System.Threading.Tasks;
using Newtonsoft.Json;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class GetProductsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var productsApi = DiHandler.GetService<ProductsApi>();
            var topUpProducts = await productsApi.ProductsTopupGetAsync(0,100);

            await data.SendBack(new MessageData("productsResponse", JsonConvert.SerializeObject(topUpProducts), A_HOUR));
        }
    }
}