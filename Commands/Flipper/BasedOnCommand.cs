using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class BasedOnCommand : Command
    {

        static RestClient SkyFlipperHost = new RestClient("http://"+SimplerConfig.Config.Instance["SKYFLIPPER_HOST"]);
        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            System.Console.WriteLine(uuid);
            using (var context = new HypixelContext())
            {
                var auction = AuctionService.Instance.GetAuction(uuid);
                if (auction == null)
                    throw new CoflnetException("auction_unkown", "not found");
                    /*
                if (FlipperService.Instance.relevantAuctionIds.TryGetValue(auction.UId, out List<long> ids))
                {
                    return data.SendBack(data.Create("basedOnResp", context.Auctions.Where(a => ids.Contains(a.UId)).Select(a => new Response()
                    {
                        uuid = a.Uuid,
                        highestBid = a.HighestBidAmount,
                        end = a.End
                    }).ToList(), 120));
                }*/

                var response = await SkyFlipperHost.ExecuteAsync(new RestRequest("flip/{uuid}/based").AddParameter("uuid",uuid, ParameterType.UrlSegment));// Flipper.FlipperEngine.Instance.GetRelevantAuctionsCache(auction, context);
                var result = JsonConvert.DeserializeObject<List<SaveAuction>>(response.Content);
                await data.SendBack(data.Create("basedOnResp", result
                            .Select(a => new Response()
                            {
                                uuid = a.Uuid,
                                highestBid = a.HighestBidAmount,
                                end = a.End,
                                ItemName = a.ItemName
                            }),
                            A_HOUR));
            }
        }
        [DataContract]
        public class Response
        {
            [DataMember(Name = "uuid")]
            public string uuid;
            [DataMember(Name = "highestBid")]
            public long highestBid;
            [DataMember(Name = "end")]
            public System.DateTime end;
            [DataMember(Name = "name")]
            public string ItemName { get; set; }
        }
    }
}