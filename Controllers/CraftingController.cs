using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;
using RestSharp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using  Coflnet.Sky.Crafts.Models;
using Newtonsoft.Json;

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
        public async Task<IEnumerable<ProfitableCraft>> GetProfitable(string profile = null)
        {
            var response = await  client.ExecuteAsync(new RestRequest("Crafts"));
            var crafts = JsonConvert.DeserializeObject<List<ProfitableCraft>>(response.Content);
            return crafts;
        }
    }
}