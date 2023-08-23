using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public partial class BasedOnCommand : Command
    {

        public override async Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            System.Console.WriteLine(uuid);
            using (var context = new HypixelContext())
            {
                var auction = AuctionService.Instance.GetAuction(uuid);
                if (auction == null)
                    throw new CoflnetException("auction_unkown", "Auction not found yet, please try again in a few seconds");
                List<SaveAuction> result = await data.GetService<FlipperService>().GetReferences(uuid);
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
    }
}