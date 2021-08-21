using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class PlayerAuctionsCommand : PaginatedRequestCommand<AuctionResult>
    {
        public override string ResponseCommandName => "playerAuctionsResponse";

        public override IEnumerable<AuctionResult> GetAllElements(string selector,int amount,int offset)
        {
            using(var context = new HypixelContext())
            {
                var auctions = context.Auctions
                        .Where(a=>a.SellerId == context.Players.Where(p=>p.UuId == selector).Select(p=>p.Id).FirstOrDefault())
                        .OrderByDescending(a=>a.End)
                        .Skip(offset)
                        .Take(amount)
                        .ToList();

                return auctions.Select(a=>new AuctionResult(a));
            }
        }
  
    }
}
