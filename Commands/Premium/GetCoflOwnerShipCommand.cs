using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;

namespace hypixel
{
    public class GetCoflOwnerShipCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var productsApi = new UserApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            var userData = await productsApi.UserUserIdGetAsync(data.UserId.ToString());
            await data.SendBack(data.Create("coflBalance", userData.Owns, A_MINUTE / 2));
        }
    }

}