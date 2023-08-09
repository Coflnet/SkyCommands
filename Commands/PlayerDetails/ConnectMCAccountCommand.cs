using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class ConnectMCAccountCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            var userId = data.UserId;

            var player = await PlayerService.Instance.GetPlayer(uuid);
            if (player == default(Player))
                throw new CoflnetException("unkown_player", "This player was not found");

            var restResponse = await data.GetService<McAccountService>().ConnectAccount(userId.ToString(),uuid);
            await data.SendBack(data.Create("connectMc", restResponse));
        }
    }
}