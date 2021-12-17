using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky;
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

        private static int GetItemId(string itemTag, bool forceget = true)
        {
            return ItemDetails.Instance.GetItemIdForName(itemTag, forceget);
        }

        /// <summary>
        /// Gets the latest known buy and sell price for an item per type 
        /// </summary>
        /// <param name="itemTag">The itemTag to get prices for</param>
        /// <param name="count">For how many items the price should be retrieved</param>add 
        /// <returns></returns>
        public async Task<CurrentPrice> GetCurrentPrice(string itemTag, int count = 1)
        {
            var itemTask = ItemDetails.Instance.GetDetailsWithCache(itemTag);
            int id = GetItemId(itemTag, false);
            if (id == 0)
                return new CurrentPrice() { Available = -1 };
            var item = await itemTask;
            if (item.IsBazaar)
            {
                var product = await context.BazaarPrices
                    .Where(p => p.ProductId == itemTag)
                    .OrderByDescending(p => p.Id)
                    .Include(p => p.BuySummery)
                    .Include(p => p.QuickStatus)
                    .FirstOrDefaultAsync();
                if (count == 1)
                    return new CurrentPrice() { Buy = product.QuickStatus.BuyPrice, Sell = product.QuickStatus.SellPrice };

                return new CurrentPrice()
                {
                    Buy = GetBazaarCostForCount(product.BuySummery, count),
                    Sell = product.QuickStatus.SellPrice,
                    Available = product.BuySummery.Sum(b => b.Amount)
                };
            }
            else
            {
                var filter = new Dictionary<string, string>();
                var lowestBins = await ItemPrices.GetLowestBin(itemTag, filter, count <= 2 ? 2 : count);
                if (lowestBins.Count == 0)
                {
                    var sumary = await GetSumary(itemTag, filter);
                    return new CurrentPrice() { Buy = sumary.Med, Sell = sumary.Min };
                }
                var cost = count == 1 ? lowestBins.FirstOrDefault().Price : lowestBins.Sum(a => a.Price);
                return new CurrentPrice() { Buy = cost, Sell = lowestBins.FirstOrDefault()?.Price * 0.99 ?? 0, Available = lowestBins.Count };
            }

        }

        public double GetBazaarCostForCount(List<dev.BuyOrder> orders, int count)
        {
            var totalCost = 0d;
            var alreadyAddedCount = 0;
            foreach (var sellOrder in orders)
            {
                var toTake = sellOrder.Amount + alreadyAddedCount > count ? count - alreadyAddedCount : sellOrder.Amount;
                totalCost += sellOrder.PricePerUnit * toTake;
                alreadyAddedCount += toTake;
                if (alreadyAddedCount >= count)
                    return totalCost;
            }

            return -1;
        }
    }
}
