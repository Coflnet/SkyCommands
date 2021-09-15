using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class PricesService
    {
        private HypixelContext context;
        private FilterEngine FilterEngine = new FilterEngine();

        /// <summary>
        /// Creates a new 
        /// </summary>
        /// <param name="context"></param>
        public PricesService(HypixelContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Get sumary of price
        /// </summary>
        /// <param name="itemTag"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public async Task<PriceSumary> GetSumary(string itemTag, Dictionary<string, string> filter)
        {
            var id = ItemDetails.Instance.GetItemIdForName(itemTag);
            var minTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            var mainSelect = context.Auctions.Where(a => a.ItemId == id && a.End < DateTime.Now && a.End > minTime && a.HighestBidAmount > 0);
            var auctions = (await FilterEngine.AddFilters(mainSelect, filter)
                            .Select(a => a.HighestBidAmount).ToListAsync()).OrderByDescending(p => p).ToList();
            var mode = auctions.GroupBy(a => a).OrderByDescending(a => a.Count()).FirstOrDefault();
            return new PriceSumary()
            {
                Max = auctions.FirstOrDefault(),
                Med = auctions.Count > 0 ? auctions.Skip(auctions.Count() / 2).FirstOrDefault() : 0,
                Min = auctions.LastOrDefault(),
                Mean = auctions.Count > 0 ? auctions.Average() : 0,
                Mode = mode?.Key ?? 0,
                Volume = auctions.Count > 0 ? auctions.Count() : 0
            };
        }
    }
}
