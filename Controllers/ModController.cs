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
        private PricesService priceService;

        public ModController(HypixelContext db, PricesService pricesService)
        {
            this.db = db;
            priceService = pricesService;
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
        public async Task<string> ItemDescription(string uuid, int count = 1)
        {
            if (uuid.Length < 32 && uuid.Length != 12)
            {
                if (ItemDetails.Instance.GetItemIdForName(uuid) == 0)
                    throw new CoflnetException("invalid_id", "the passed id does not map to an item");
                var median = await priceService.GetSumary(uuid, new Dictionary<string, string>());
                return $"Median sell for {count} is {FormatPrice(median.Med)}";
            }

            var lookupId = hypixel.NBT.UidToLong(uuid.Length == 12 ? uuid : uuid.Substring(24));
            var key = NBT.GetLookupKey("uId");
            var auctions = await db.Auctions.Where(a => a.NBTLookup.Where(l => l.KeyId == key && l.Value == lookupId).Any()).Include(a => a.Bids).OrderByDescending(a => a.End).ToListAsync();
            var lastSell = auctions.Where(a => a.End < System.DateTime.Now).FirstOrDefault();
            long med = await GetMedian(lastSell);
            return $"Sold {auctions.Count} times\n"
                + (lastSell == null ? "" : $"last sold for {FormatPrice(lastSell.HighestBidAmount)} to {await PlayerSearch.Instance.GetNameWithCacheAsync(lastSell.Bids.FirstOrDefault()?.Bidder)}")
                + (auctions.Count == 0 ? "" : $"Median {FormatPrice(med)}");
        }

        private static async Task<long> GetMedian(SaveAuction lastSell)
        {
            var references = await BasedOnCommand.GetReferences(lastSell.Uuid);
            var med = references.OrderByDescending(r => r.HighestBidAmount).Skip(references.Count / 2).FirstOrDefault()?.HighestBidAmount ?? 0;
            return med;
        }

        private static string FormatPrice(long price)
        {
            return string.Format("{0:n0}", price);
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

