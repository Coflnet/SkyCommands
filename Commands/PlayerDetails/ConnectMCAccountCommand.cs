using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Coflnet.Sky.Commands;
using RestSharp;

namespace hypixel
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

            var restResponse = await McAccountService.Instance.ConnectAccount(userId.ToString(),uuid);
            await data.SendBack(new MessageData("connectMc", restResponse));
        }
    }
}