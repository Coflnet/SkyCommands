using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private HypixelContext db;

        public ModController(HypixelContext db)
        {
            this.db = db;
        }


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

        /// <summary>
        /// Returns extra information for an item
        /// </summary>
        [Route("item/{uuid}")]
        [HttpGet]
        public async Task<string> ItemDescription(string uuid)
        {
            if (uuid.Length < 32 && uuid.Length != 12)
                throw new CoflnetException("invalid_uuid", "the passed uuid is not valid");

            var lookupId = hypixel.NBT.UidToLong(uuid.Length == 12 ? uuid : uuid.Substring(24));
            var key = NBT.GetLookupKey("uId");
            var auctions = await db.Auctions.Where(a => a.NBTLookup.Where(l => l.KeyId == key && l.Value == lookupId).Any()).Include(a => a.Bids).OrderByDescending(a => a.End).ToListAsync();
            var lastSell = auctions.Where(a => a.End < System.DateTime.Now).FirstOrDefault();
            return $"Sold {auctions.Count} times\n"
                + (lastSell == null ? "" : $"last sold for {string.Format("{0:n0}", lastSell.HighestBidAmount)} to {await PlayerSearch.Instance.GetNameWithCacheAsync(lastSell.Bids.FirstOrDefault()?.Bidder)}");
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

