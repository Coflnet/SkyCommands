using System.Threading.Tasks;
using hypixel;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

namespace Coflnet.Sky.Commands.MC
{
    public class ClickedCommand : McCommand
    {
        public async override Task Execute(MinecraftSocket socket, string arguments)
        {

            var command = JsonConvert.DeserializeObject<string>(arguments);

            if (command.Split(" ")[0] != "/viewauction")
            {
                return;
            }

            var auctionUUID = command.Split(" ")[1];

            //var affected = SubscribeEngine.Instance.Unsubscribe(userId, args.Topic,args.Type).Result;
            var request = new RestRequest("Tracker/flipEvent/{auctionUUID}", Method.POST)
                .AddJsonBody(new Arguments() { FlipTrackerEvent = FlipEventType.FLIP_CLICK, PlayerUUID = socket.McId })
                .AddUrlSegment("auctionUUID", auctionUUID);
            var response = await TrackerClient.Client.ExecuteAsync(request);
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("playerUUID")]
            public string PlayerUUID;
            [Key("flipEventType")]
            public FlipEventType FlipTrackerEvent;

        }

        public enum FlipEventType
        {
            FLIP_RECEIVE = 1,
            FLIP_CLICK = 2,
            PURCHASE_START = 4,
            PURCHASE_CONFIRM = 8,
            AUCTION_SOLD = 16
        }
    }
}