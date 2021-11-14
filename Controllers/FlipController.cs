using System;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Filter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RestSharp;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Endpoints for flips
    /// </summary>
    [ApiController]
    [Route("api/flip")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class FlipController : ControllerBase
    {
        private IConfiguration config;

        /// <summary>
        /// Creates a new instance of <see cref="FlipController"/>
        /// </summary>
        /// <param name="config"></param>
        public FlipController(IConfiguration config)
        {
            this.config = config;
        }

        /// <summary>
        /// The last time an update was loaded (cached for 30min)
        /// You should only look at the second part
        /// </summary>
        /// <returns></returns>
        [Route("update/when")]
        [HttpGet]
        public async Task<DateTime> GetFlipTime()
        {
            return await new NextUpdateRetriever().Get();
        }

        /// <summary>
        /// Shows you the available settings options for the socket comand subFlip,
        /// Doesn't currently actually do anything.
        /// </summary>
        /// <returns>The default settings for modsocket v1</returns>
        [Route("settings/options")]
        [HttpGet]
        public FlipSettings SeeOptions()
        {
            return Sky.Commands.MC.MinecraftSocket.DEFAULT_SETTINGS;
        }
    }
}

