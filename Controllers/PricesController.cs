using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using hypixel;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for retrieving prices
    /// </summary>
    [ApiController]
    [Route("api")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class PricesController : ControllerBase
    {
        private PricesService priceService;

        public PricesController(PricesService pricesService)
        {
            priceService = pricesService;
        }
        /// <summary>
        /// Aggregated sumary of item prices for the last day
        /// </summary>
        /// <param name="itemTag">The item tag you want prices for</param>
        /// <param name="query">Filter query</param>
        /// <returns></returns>
        [Route("item/price/{itemTag}")]
        [HttpGet]
        public Task<PriceSumary> GetSumary(string itemTag, [FromQuery] IDictionary<string, string> query)
        {
            return priceService.GetSumary(itemTag, new Dictionary<string, string>(query));
        }

        /// <summary>
        /// Gets the lowest bin by item type
        /// </summary>
        /// <param name="itemTag">The tag of the item to search for bin</param>
        /// <param name="query"></param>
        /// <returns></returns>
        [Route("item/price/{itemTag}/bin")]
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<ActionResult<BinResponse>> GetLowestBin(string itemTag, [FromQuery] IDictionary<string, string> query)
        {
            var result = await ItemPrices.GetLowestBin(itemTag, new Dictionary<string, string>(query));
            return Ok(new BinResponse(result.FirstOrDefault()?.Price ?? 0, result.FirstOrDefault()?.Uuid, result.LastOrDefault()?.Price ?? 0));
        }

        /// <summary>
        /// Gets the current (latest known) price for an item
        /// </summary>
        /// <param name="itemTag">The tag of the item/param>
        /// <param name="query"></param>
        /// <returns></returns>
        [Route("item/price/{itemTag}/current")]
        [HttpGet]
        [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
        public async Task<CurrentPrice> GetCurrentPrice(string itemTag)
        {
            return await priceService.GetCurrentPrice(itemTag);
        }

        /// <summary>
        /// Returns all available filters with all available options
        /// </summary>
        /// <returns></returns>
        [Route("filter/options")]
        [HttpGet]
        [ResponseCache(Duration = 3600 * 6, Location = ResponseCacheLocation.Any, NoStore = false)]
        public List<FilterOptions> GetFilterOptions()
        {
            var fe = new Sky.Filter.FilterEngine();
            return fe.AvailableFilters.Select(f => new FilterOptions(f)).ToList();
        }

        /// <summary>
        /// Lowest bin response
        /// </summary>
        [DataContract]
        public class BinResponse
        {
            /// <summary>
            /// The lowest bin price
            /// </summary>
            [DataMember(Name = "lowest")]
            public long Lowest;
            /// <summary>
            /// The lowest bin auction uuid
            /// </summary>
            [DataMember(Name = "uuid")]
            public string Uuid;
            /// <summary>
            /// The price of the second lowest bin
            /// </summary>
            [DataMember(Name = "secondLowest")]
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