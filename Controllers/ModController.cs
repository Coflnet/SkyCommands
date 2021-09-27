using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Hypixel.Controller
{
    /// <summary>
    /// Special endpoints for mods.
    /// Returns information about mod related things. e.g. available socket commands for a help text
    /// </summary>
    [ApiController]
    [Route("api/mod")]
    [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any, NoStore = false)]
    public class ModController : ControllerBase
    {
        /// <summary>
        /// Returns a list of available server-side commands
        /// </summary>
        [Route("commands")]
        [HttpGet]
        public IEnumerable<CommandListEntry> GetSumary()
        {
            return new List<CommandListEntry>()
            {
                new CommandListEntry("test","Returns a test response")
            };
        }

        public class CommandListEntry
        {
            public string SubCommand;
            public string Description;

            public CommandListEntry(string subCommand, string description)
            {
                SubCommand = subCommand;
                Description = description;
            }
        }

    }
}

