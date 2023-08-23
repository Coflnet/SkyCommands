using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class GetCoflOwnerShipCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var productsApi = DiHandler.GetService<UserApi>();
            var userData = await productsApi.UserUserIdGetAsync(data.UserId.ToString());
            await data.SendBack(data.Create("coflBalance", userData.Owns, A_MINUTE / 2));
        }
    }

}