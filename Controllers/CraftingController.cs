using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using RestSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for crafting related data
    /// </summary>
    [ApiController]
    [Route("api/craft")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class CraftingController : ControllerBase
    {
        private static RestClient client = null;
        public CraftingController(IConfiguration config)
        {
            if (client == null)
                client = new RestClient("http://" + config["CRAFTS_HOST"]);
        }

        [Route("profit")]
        [HttpGet]
        public async Task<string> GetProfitable(string playerProfile = null)
        {
            var response = await  client.ExecuteAsync(new RestRequest("Crafts"));
            return response.Content;
        }
    }
}