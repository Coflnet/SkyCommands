using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Main API endpoints
    /// </summary>
    [ApiController]
    [Route("api")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class ApiController : ControllerBase
    {
        /// <summary>
        /// Searches through all items
        /// </summary>
        /// <param name="searchVal">The search term to search for</param>
        /// <returns>An array of search results matching the searchValue</returns>
        [Route("item/search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult<List<SearchService.SearchResultItem>>> SearchItem(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("itemSearch", searchVal);
            return Ok(result);
        }

        /// <summary>
        /// Full search, includes items, players and enchantments
        /// </summary>
        /// <param name="searchVal">The search term to search for</param>
        /// <returns></returns>
        [Route("search/{searchVal}")]
        [HttpGet]
        public async Task<ActionResult<List<SearchService.SearchResultItem>>> FullSearch(string searchVal)
        {
            var result = await Server.ExecuteCommandWithCache<string, List<SearchService.SearchResultItem>>("fullSearch", searchVal);
            return Ok(result);
        }


    }
}

