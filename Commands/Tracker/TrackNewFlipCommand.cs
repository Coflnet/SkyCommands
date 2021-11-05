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

                var request = new RestRequest("Tracker/newFlip/{auctionUUID}", Method.POST)
                    .AddJsonBody(args)
                    .AddUrlSegment("auctionUUID", args.AuctionUUID);

                var response = await TrackerClient.Client.ExecuteAsync(request);

                await data.SendBack(new MessageData("newFlip", null));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("auctionUUID")]
            public string AuctionUUID;
            [Key("targetPrice")]
            public int TargetPrice;
            [Key("finderType")]
            public FinderType FinderType;

        }

        public enum FinderType
        {
            FLIPPER = 1,
            LOWEST_BIN = 2,
            SNIPER = 4,
            AI = 8
        }
    }
}
