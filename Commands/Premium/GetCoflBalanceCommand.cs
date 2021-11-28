using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;

namespace hypixel
{
    public class GetCoflBalanceCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var productsApi = new UserApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            var userData = await productsApi.UserUserIdGetAsync(data.UserId.ToString());
            await data.SendBack(data.Create("coflBalance", userData.Balance, A_MINUTE / 6));
        }
    }

}