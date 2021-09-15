

using System;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for retrieving prices
    /// </summary>
    [ApiController]
    [Route("api")]
    public class PricesController : ControllerBase
    {
        /// <summary>
        /// Aggregated sumary of item prices for the last day
        /// </summary>
        /// <param name="itemTag">The item tag you want prices for</param>
        /// <returns></returns>
        [Route("item/price/{itemTag}")]
        [HttpGet]
        public async Task<ActionResult<PriceSumaryCommand.Result>> GetSumary(string itemTag)
        {
            var result = await Server.ExecuteCommandWithCache<string, PriceSumaryCommand.Result>("priceSum", itemTag);
            return Ok(result);
        }

        /// <summary>
        /// Gets the lowest bin by item type
        /// </summary>
        /// <param name="itemTag">The tag of the item to search for bin</param>
        /// <param name="tier">The tier aka rarity of the item. Allows to filter pets and recombobulated items</param>
        /// <returns></returns>
        [Route("item/price/{itemTag}/bin")]
        [HttpGet]
        public async Task<ActionResult<BinResponse>> GetLowestBin(string itemTag, [FromQuery] Tier? tier)
        {
            var result = await ItemPrices.GetLowestBin(itemTag, tier ?? Tier.UNCOMMON);
            return Ok(new BinResponse(result.FirstOrDefault()?.Price ?? 0, result.FirstOrDefault()?.Uuid, result.LastOrDefault()?.Price ?? 0));
        }

        /// <summary>
        /// Lowest bin response
        /// </summary>
        public class BinResponse
        {
            /// <summary>
            /// The lowest bin price
            /// </summary>
            public long Lowest;
            /// <summary>
            /// The lowest bin auction uuid
            /// </summary>
            public string Uuid;
            /// <summary>
            /// The price of the second lowest bin
            /// </summary>
            public long SecondLowest;

            /// <summary>
            /// Creates a new instance of <see cref="BinResponse"/>
            /// </summary>
            /// <param name="lowest"></param>
            /// <param name="uuid"></param>
            /// <param name="secondLowest"></param>
            public BinResponse(long lowest, string uuid, long secondLowest)
            {
                Lowest = lowest;
                Uuid = uuid;
                SecondLowest = secondLowest;
            }
        }
    }
}

