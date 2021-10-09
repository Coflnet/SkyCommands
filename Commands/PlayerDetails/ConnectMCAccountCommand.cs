using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using RestSharp;

namespace hypixel
{
    public class ConnectMCAccountCommand : Command
    {
        RestClient mcAccountClient = new RestClient("http://"+ SimplerConfig.Config.Instance["MCCONNECT_HOST"]);

        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            var userId = data.UserId;

            var player = await PlayerService.Instance.GetPlayer(uuid);
            if (player == default(Player))
                throw new CoflnetException("unkown_player", "This player was not found");

            var restResponse = await mcAccountClient.ExecuteAsync(new RestRequest("Connect​/user​/{userId}").AddUrlSegment("userId",userId).AddQueryParameter("mcUuid",uuid));

            await data.SendBack(new MessageData("connectMc", restResponse.Content));
        }
    }
}