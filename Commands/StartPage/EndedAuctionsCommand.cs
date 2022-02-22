using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class EndedAuctionsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            /* TODO: get this directly from an updater
            if(BinUpdater.SoldLastMin.Count > 0)
            {
                var recentSold = BinUpdater.SoldLastMin.Take(50)
                    .Select(a => new AuctionResult(a))
                    .Select(AuctionService.Instance.GuessMissingProperties)
                    .ToList();

                return data.SendBack(data.Create("endedAuctions",recentSold , A_MINUTE));
            }*/

            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(30); 
                var end = System.DateTime.Now;
                var highVolumeIds = new System.Collections.Generic.HashSet<int>()
                {
                    ItemDetails.Instance.GetItemIdForName("ENCHANTED_BOOK"),
                    ItemDetails.Instance.GetItemIdForName("GRAPPLING_HOOK"),
                    ItemDetails.Instance.GetItemIdForName("KISMET_FEATHER"),

                };
                var pages = context.Auctions.Where(a => (highVolumeIds.Contains(a.ItemId) || a.Id > context.Auctions.Max(a=>a.Id) - 100_000 )&& a.End < end)
                    .OrderByDescending(a => a.Id)
                    .Select(p => new AuctionResult(p))
                    .Take(100)
                    .ToList()
                    .OrderByDescending(a => a.End)
                    .Take(30)
                    .Select(AuctionService.Instance.GuessMissingProperties)
                    .ToList();
                return data.SendBack(data.Create("endedAuctions", pages, A_MINUTE));
            }
        }
    }
}
