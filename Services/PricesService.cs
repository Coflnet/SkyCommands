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
            int id = GetItemId(itemTag);
            var minTime = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            var mainSelect = context.Auctions.Where(a => a.ItemId == id && a.End < DateTime.Now && a.End > minTime && a.HighestBidAmount > 0);
            filter["ItemId"] = id.ToString();
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

        private static int GetItemId(string itemTag)
        {
            return ItemDetails.Instance.GetItemIdForName(itemTag);
        }

        /// <summary>
        /// Gets the latest known buy and sell price for an item per type 
        /// </summary>
        /// <param name="itemTag">The itemTag to get prices for</param>add 
        /// <returns></returns>
        public async Task<CurrentPrice> GetCurrentPrice(string itemTag)
        {
            var itemTask = ItemDetails.Instance.GetDetailsWithCache(itemTag);
            int id = GetItemId(itemTag);
            var item = await itemTask;
            if (item.IsBazaar)
            {
                var quickStatus = await context.BazaarPrices
                    .Where(p => p.ProductId == itemTag)
                    .OrderByDescending(p => p.Id)
                    .Select(p => p.QuickStatus)
                    .FirstOrDefaultAsync();
                return new CurrentPrice() { Buy = quickStatus.BuyPrice, Sell = quickStatus.SellPrice };
            }
            else
            {
                var filter = new Dictionary<string, string>();
                var lowestBins = await ItemPrices.GetLowestBin(itemTag, filter);
                if (lowestBins.Count == 0)
                {
                    var sumary = await GetSumary(itemTag, filter);
                    return new CurrentPrice() { Buy = sumary.Med, Sell = sumary.Min };
                }
                var lowestPrice = lowestBins.FirstOrDefault()?.Price ?? 0;
                return new CurrentPrice() { Buy = lowestPrice, Sell = lowestPrice == 0 ? 0 : lowestPrice * 0.99 };
            }

        }
    }
}
