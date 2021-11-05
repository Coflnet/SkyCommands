using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

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

                var request = new RestRequest("Tracker/newFlip/{auctionUUID}", Method.POST)
                    .AddJsonBody(args)
                    .AddUrlSegment("auctionUUID", args.auctionUUID);

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
            public DateTime timestamp;

        }
    }
}
