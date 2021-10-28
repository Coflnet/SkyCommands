using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace hypixel
{
    public class TrackNewFlipCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();
                var userId = data.UserId;

                var request = new RestRequest("Tracker/newFlip", Method.POST)
                    .AddJsonBody(new FlipTrackingModel() { auctionUUID = args.auctionUUID, targetPrice = args.targetPrice, finderType = args.finderType, timestamp = args.timestamp });

                await data.SendBack(new MessageData("newFlip", null));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("auctionUUID")]
            public string auctionUUID;
            [Key("targetPrice")]
            public int targetPrice;
            [Key("finderType")]
            public string finderType;
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
    }
}
