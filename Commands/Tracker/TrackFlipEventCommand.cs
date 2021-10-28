using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

                var request = new RestRequest("Tracker/flipEvent", Method.POST)
                    .AddJsonBody(new FlipTrackingModel() { playerUUID = args.playerUUID, auctionUUID = args.auctionUUID, flipTrackerEvent = args.flipTrackerEvent, timestamp = args.timestamp });

                await data.SendBack(new MessageData("flipEvent", null));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("playerUUID")]
            public string playerUUID;
            [Key("auctionUUID")]
            public int auctionUUID;
            [Key("event")]
            public FlipTrackerEvent flipTrackerEvent;
            [Key("timestamp")]
            public long timestamp
            {
                set
                {
                    if (value == 0)
                    {
                        End = DateTime.Now;
                    }
                    else
                        End = value.ThisIsNowATimeStamp();
                }
                get
                {
                    return End.ToUnix();
                }
            }

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
