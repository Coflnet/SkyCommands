using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Coflnet.Sky.Core;
using System;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class EndedAuctionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var response = await IndexerClient.RecentlyEnded();
            if (response != null)
            {
                await data.SendBack(data.Create("endedAuctions", response.OrderByDescending(a => a.End), A_MINUTE));
                return;
            }
            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(30);
                var end = DateTime.Now;
                var start = end - TimeSpan.FromMinutes(1.2);
                var highVolumeIds = new System.Collections.Generic.HashSet<int>()
                {
                    ItemDetails.Instance.GetItemIdForTag("GOD_POTION_2"),
                    ItemDetails.Instance.GetItemIdForTag("GRAPPLING_HOOK"),
                    ItemDetails.Instance.GetItemIdForTag("KAT_FLOWER"),
                    ItemDetails.Instance.GetItemIdForTag("ASPECT_OF_THE_DRAGON")

                };
                var pages = context.Auctions.Where(a => (highVolumeIds.Contains(a.ItemId)) && a.End < end && a.End > start)
                    .OrderByDescending(a => a.Id)
                    .Select(p => new AuctionResult(p))
                    .Take(100)
                    .ToList()
                    .OrderByDescending(a => a.End)
                    .Take(30)
                    .Select(AuctionService.Instance.GuessMissingProperties)
                    .ToList();
                await data.SendBack(data.Create("endedAuctions", pages, A_MINUTE));
            }
        }
    }
}
