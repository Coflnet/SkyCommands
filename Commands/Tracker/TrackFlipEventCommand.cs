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
                var userId = data.UserId;

                var request = new RestRequest("Tracker/flipEvent/{auctionUUID}", Method.POST)
                    .AddJsonBody(args)
                    .AddUrlSegment("auctionUUID", args.auctionUUID);

                await data.SendBack(new MessageData("flipEvent", null));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("playerUUID")]
            public string playerUUID;
            [Key("auctionUUID")]
            public string auctionUUID;
            [Key("event")]
            public FlipTrackerEvent flipTrackerEvent;

        }

        public enum FlipTrackerEvent
        {
            PURCHASE_START = 1,
            PURCHASE_CONFIRM = 2,
            FLIP_RECEIVE = 4,
            FLIP_CLICK = 8,
            AUCTION_SOLD = 16
        }
    }
}
