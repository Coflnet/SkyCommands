using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public partial class BasedOnCommand : Command
{

    public override async Task Execute(MessageData data)
    {
        var uuid = data.GetAs<string>();
        Console.WriteLine(uuid);
        var auction = AuctionService.Instance.GetAuction(uuid)
            ?? throw new CoflnetException("auction_unkown", "Auction not found yet, please try again in a few seconds");

        try
        {

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
        catch (System.Exception)
        {
            throw new CoflnetException("turned_off", "This feature is currently turned disabled while we are working on getting more servers up and running. Sorry about that, you can use our mod in game to get an estimate of the price.");
        }
    }
}