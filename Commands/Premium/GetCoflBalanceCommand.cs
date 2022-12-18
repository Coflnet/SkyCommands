using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class GetCoflBalanceCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var productsApi = DiHandler.GetService<UserApi>();
            var userData = await productsApi.UserUserIdGetAsync(data.UserId.ToString());
            await data.SendBack(data.Create("coflBalance", userData.Balance));
        }
    }

}