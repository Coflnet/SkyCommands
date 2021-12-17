using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using Coflnet.Sky.Commands.Shared;

namespace hypixel
{
    public partial class BasedOnCommand : Command
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
                    throw new CoflnetException("auction_unkown", "Auction not found yet, please try again in a few seconds");
                List<SaveAuction> result = await GetReferences(uuid);
                await data.SendBack(data.Create("basedOnResp", result
                            .Select(a => new BasedOnCommandResponse()
                            {
                                uuid = a.Uuid,
                                highestBid = a.HighestBidAmount,
                                end = a.End,
                                ItemName = a.ItemName
                            }),
                            A_HOUR));
            }
        }

        public static async Task<List<SaveAuction>> GetReferences(string uuid)
        {
            var response = await SkyFlipperHost.ExecuteAsync(new RestRequest("flip/{uuid}/based").AddParameter("uuid", uuid, ParameterType.UrlSegment));
            var result = JsonConvert.DeserializeObject<List<SaveAuction>>(response.Content);
            return result;
        }
    }
}