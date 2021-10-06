

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using hypixel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Hypixel.Controller
{
    [ApiController]
    [Route("api")]
    public class AuctionsController : ControllerBase
    {
        AuctionService auctionService;
        HypixelContext context;

        public AuctionsController(AuctionService auctionService, HypixelContext context)
        {
            this.auctionService = auctionService;
            this.context = context;
        }

        /// <summary>
        /// Retrieve details of a specific auction
        /// </summary>
        /// <param name="auctionUuid">The uuid of the auction you want the details for</param>
        /// <returns></returns>
        [Route("auction/{auctionUuid}")]
        [HttpGet]
        [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult<SaveAuction>> getAuctionDetails(string auctionUuid)
        {
            var result = await auctionService.GetAuctionAsync(auctionUuid, auction => auction
                        .Include(a => a.Enchantments)
                        .Include(a => a.NbtData)
                        .Include(a => a.Bids));

            return Ok(result);
        }

        /// <summary>
        /// Get the 10 (or how many are available) lowest bins
        /// </summary>
        /// <param name="itemTag">The itemTag to get bins for</param>
        /// <param name="query">Filters for the auctions</param>
        /// <returns></returns>
        [Route("auctions/tag/{itemTag}/active/bin")]
        [HttpGet]
        //[ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult<List<SaveAuction>>> GetLowestBins(string itemTag, [FromQuery] IDictionary<string, string> query)
        {
            var itemId = ItemDetails.Instance.GetItemIdForName(itemTag);
            var filter = new Dictionary<string, string>(query);
            int page = 0;
            if (filter.ContainsKey("page"))
            {
                int.TryParse(filter["page"], out page);
                filter.Remove("page");
            }
            var pageSize = 10;
            var result = await new FilterEngine().AddFilters(context.Auctions
                        .Where(a => a.ItemId == itemId && a.End > DateTime.Now && a.HighestBidAmount == 0 && a.Bin), filter)
                        .Include(a => a.Enchantments)
                        .Include(a => a.NbtData)
                        .OrderBy(a => a.StartingBid)
                        .Skip(page * pageSize)
                        .Take(pageSize).ToListAsync();

            return Ok(result);
        }
    }
}

