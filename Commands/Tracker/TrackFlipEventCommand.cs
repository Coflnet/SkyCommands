using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class TrackFlipEventCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();

                var request = new RestRequest("Tracker/flipEvent/{auctionUUID}", Method.POST)
                    .AddJsonBody(args)
                    .AddUrlSegment("auctionUUID", args.AuctionUUID);

                var response = await TrackerClient.Client.ExecuteAsync(request);

                await data.SendBack(new MessageData("flipEvent", null));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("playerUUID")]
            public string PlayerUUID;
            [Key("auctionUUID")]
            public string AuctionUUID;
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
