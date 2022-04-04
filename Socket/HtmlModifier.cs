using System;
using System.Text;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Collections.Specialized;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using static Coflnet.Sky.Core.ItemPrices;

namespace Coflnet.Sky.Commands
{
    public class HtmlModifier
    {

        const string defaultText = "Browse over 100 million auctions, and the bazzar of Hypixel SkyBlock";
        const string defaultTitle = "Skyblock Auction House History";
        const string DETAILS_START = @"<noscript>";
        public static async Task<string> ModifyContent(string path, byte[] contents, RequestContext res)
        {
            string parameter = "";
            var urlParts = path.Split('/', '?', '#');
            if (urlParts.Length > 2)
                parameter = urlParts[2];
            string description = "Browse over 300 million auctions, and the bazaar of Hypixel SkyBlock.";
            string longDescription = null;
            string title = defaultTitle;
            string imageUrl = "https://sky.coflnet.com/logo192.png";
            string keyword = "";
            System.Collections.Specialized.NameValueCollection args = HttpUtility.ParseQueryString(path.Split('?').LastOrDefault());
            string refBy = null;
            if (args["refId"] != null)
                refBy = await ReferalService.Instance.GetUserName(args["refId"]);

            var start = Encoding.UTF8.GetString(contents).Split("<title>");
            var headerStart = start[0] + "<title>";
            var parts = start[1].Split("</head>");
            string header = parts.First();
            string html = parts.Last().Substring(0, parts.Last().Length - 14);


            if (path.StartsWith("/p/"))
                return res.RedirectSkyblock(parameter, "player");
            if (path.StartsWith("/a/"))
                return res.RedirectSkyblock(parameter, "auction");
            if (path == "/item/" || path == "/item")
                return res.RedirectSkyblock();

            if(path == "/premium")
                description = "See available premium options to support this project";
            if(path == "/crafts")
                description = "List of profitable crafts based on current ah and bazaar prices";
            if(path == "/ref")
                description = "Our referral system allows you to get a reward for inviting others";
            if(path == "/lowSupply")
                description = "Items that are in low supply on the auction house";
            

            // try to fill in title
            if (path.Contains("auction/"))
            {
                await WriteStart(res, headerStart);
                // is an auction

                var result = AuctionService.Instance.GetAuctionWithSelect(parameter, auction => auction
                         .Select(a => new AuctionPreviewParams(a.Tag, a.AuctioneerId, a.ItemName, a.End, a.Bids.Count, a.Tier, a.Category, a.Bin, a.HighestBidAmount, a.UId))
                         .FirstOrDefault());
                if (result == null)
                {
                    await WriteHeader("/error", res, "This site was not found", "Error", imageUrl, null, header);
                    await res.WriteEnd(html);
                    return "";
                }

                var playerName = await PlayerSearch.Instance.GetNameWithCacheAsync(result.AuctioneerId);
                title = $"Auction for {result.ItemName} by {playerName}";
                description = await GetAuctionDescription(result, title);

                if (!string.IsNullOrEmpty(result.Tag))
                    imageUrl = "https://sky.coflnet.com/static/icon/" + result.Tag;
                else
                    imageUrl = SearchService.PlayerHeadUrl(result.AuctioneerId);

                await WriteHeader(path, res, description, title, imageUrl, keyword, header);

                longDescription = description
                    + $"<ul><li> <a href=\"/player/{result.AuctioneerId}/{playerName}\"> other auctions by {playerName} </a></li>"
                    + $" <li><a href=\"/item/{result.Tag}/{result.ItemName}\"> more auctions for {result.ItemName} </a></li></ul>";
                keyword = $"{result.ItemName},{playerName}";

            }
            else if (path.Contains("player/"))
            {
                if (parameter.Length < 30)
                {
                    var uuid = PlayerSearch.Instance.GetIdForName(parameter);
                    return res.RedirectSkyblock(uuid, "player", uuid);
                }

                await WriteStart(res, headerStart);
                keyword = await PlayerSearch.Instance.GetNameWithCacheAsync(parameter);
                title = $"{keyword} Auctions and bids";
                description = $"Auctions and bids for {keyword} in hypixel skyblock.";

                imageUrl = SearchService.PlayerHeadUrl(parameter);

                await WriteHeader(path, res, description, title, imageUrl, keyword, header);


                var auctions = GetAuctions(parameter, keyword);
                var bids = GetBids(parameter, keyword);
                await res.WriteAsync(html);
                await res.WriteAsync(DETAILS_START + $"<h1>{title}</h1>{description} " + await auctions);
                await res.WriteEnd(await bids + PopularPages());

                return "";
            }
            else if (path.Contains("item/") || path.Contains("i/"))
            {
                if (path.Contains("i/"))
                    return res.RedirectSkyblock(parameter, "item", keyword);
                if (!ItemDetails.Instance.TagLookup.ContainsKey(parameter))
                {
                    return await RedirectToItem(res, parameter, keyword);
                }
                await WriteStart(res, headerStart);
                keyword = ItemDetails.TagToName(parameter);


                var i = await ItemDetails.Instance.GetDetailsWithCache(parameter);
                path = CreateCanoicalPath(urlParts, i);
                var name = i?.Names?.FirstOrDefault();
                if (name != null)
                    keyword = name;

                title = $"{keyword} price ";
                Dictionary<string, string> filters = GetFiltersFromQuery(args);
                description = await ComputeItemSiteDescription(parameter, description, keyword, refBy, filters);

                imageUrl = "https://sky.shiiyu.moe/item/" + parameter;
                if (parameter.StartsWith("PET_") && !parameter.StartsWith("PET_ITEM") || parameter.StartsWith("POTION"))
                    imageUrl = i.IconUrl;
                await WriteHeader(path, res, description, title, imageUrl, keyword, header);

                longDescription = description
                + AddAlternativeNames(i);

                longDescription += await GetRecentAuctions(i.Tag == "Unknown" || i.Tag == null ? parameter : i.Tag);
            }
            else
            {
                if (path.Contains("/flipper"))
                {
                    title = "Skyblock AH history auction flipper";
                    description = "Free auction house item flipper for Hypixel Skyblock";
                    keyword = "flipper";
                }
                if (refBy != null)
                    description += " | invited by " + refBy;
                // unkown site, write the header
                await WriteStart(res, headerStart);
                await WriteHeader(path, res, description, title, imageUrl, keyword, header);
            }
            if (longDescription == null)
                longDescription = description;


            var newHtml = html + DETAILS_START
                        + BottomText(title, longDescription);

            await res.WriteEnd(newHtml);
            return newHtml;
        }

        private static async  Task<string> RedirectToItem(RequestContext res, string parameter, string keyword)
        {
            var upperCased = parameter.ToUpper();
            if (ItemDetails.Instance.TagLookup.ContainsKey(upperCased))
                return res.RedirectSkyblock(upperCased, "item");
            // likely not a tag
            parameter = HttpUtility.UrlDecode(parameter);
            var thread = await ItemDetails.Instance.Search(parameter, 1);
            var item = thread.FirstOrDefault();
            keyword = item?.Name;
            parameter = item?.Tag;
            return res.RedirectSkyblock(parameter, "item", keyword);
        }

        private static Dictionary<string, string> GetFiltersFromQuery(NameValueCollection args)
        {
            Dictionary<string, string> filters = null;

            if (args["itemFilter"] != null)
                filters = JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(Convert.FromBase64String(args["itemFilter"])));
            return filters;
        }

        private static async Task<string> ComputeItemSiteDescription(string parameter, string description, string keyword, string refBy, Dictionary<string, string> filters)
        {
            float price = await GetAvgPrice(parameter, filters);
            description = $"Price for item {keyword} in hypixel SkyBlock is {price.ToString("0,0.0")} on average. ";
            if (filters != null)
                description += "FILTERS ➡️ " + String.Join(", ", filters.Where(f => f.Key != "ItemId").Select(f => $"{f.Key}: {f.Value}"));
            if (refBy != null)
                description += " | invited by " + refBy;
            return description;
        }

        private static async Task<string> GetAuctionDescription(AuctionPreviewParams result, string title)
        {
            var description = "";
            if (result.Bin)
                description += $"BIN ";

            description += title;
            if (!result.Bin)
                description += $" | Highest Bid: {String.Format("{0:n0}", result.HighestBidAmount)} with {result.BidCount} Bids";
            else if (result.HighestBidAmount > 0) // sold
                using (var context = new HypixelContext())
                {
                    var buyer = await context.Auctions.Where(a => a.UId == result.UId).Select(a => a.Bids.First().Bidder).FirstOrDefaultAsync();
                    //var buyer = auction.Bids.FirstOrDefault()?.Bidder;
                    var name = await PlayerSearch.Instance.GetNameWithCacheAsync(buyer);
                    description += $" | Bought by {name} for {String.Format("{0:n0}", result.HighestBidAmount)} coins";
                }


            if (result.End > DateTime.Now)
                description += $" | Ends on {result.End.ToString("yyyy-MM-dd HH\\:mm\\:ss")}";
            else
                description += $" | Ended on {result.End.ToString("yyyy-MM-dd HH\\:mm\\:ss")}";


            return description += $" | Category: {result.Category} | Rarity: {result.Tier}";
        }

        private static async Task<float> GetAvgPrice(string tag, Dictionary<string, string> filter = null)
        {
            try
            {
                var result = await Server.ExecuteCommandWithCache<ItemSearchQuery,ItemPrices.Resonse>("pricerdicer",new ItemSearchQuery()
                {
                    Filter = filter,
                    name = tag,
                    Start = DateTime.Now - TimeSpan.FromDays(1)
                });

                var prices = result.Prices;
                if (prices == null || prices.Count == 0)
                    return 0;
                return prices.Average(a => a.Avg);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not get price for {tag} {e.Message} {e.StackTrace}");
                return -1;
            }

        }

        private static async Task WriteStart(RequestContext res, string content)
        {
            await res.WriteAsync(content);
            // res.SendChunked = true;
            res.AddHeader("cache-control", "public,max-age=" + 1800);

            res.ForceSend();
        }

        private static async Task WriteHeader(string path, RequestContext res, string description, string title, string imageUrl, string keyword, string header)
        {
            title += " | Hypixel SkyBlock AH history tracker";
            // shrink to fit
            while (title.Length > 65)
            {
                title = title.Substring(0, title.LastIndexOf(' '));
            }
            if (path.EndsWith("index.html"))
            {
                path = "";
            }


            await res.WriteAsync(header
            .Replace(defaultText, description)
            .Replace(defaultTitle, title)
            .Replace("</title>", $"</title><meta property=\"keywords\" content=\"{keyword},hypixel,skyblock,auction,history,bazaar,tracker\" />"
                + $"<meta property=\"og:image\" content=\"{imageUrl}\" />"
                + $"<meta property=\"og:url\" content=\"https://sky.coflnet.com{path}\" />"
                + $"<meta property=\"og:title\" content=\"{title}\" />"
                + $"<meta property=\"og:description\" content=\"{description}\" />"
                + $"<link rel=\"canonical\" href=\"https://sky.coflnet.com{path}\" />"
                )
                + "</head>");

            res.ForceSend();
        }

        private static string CreateCanoicalPath(string[] urlParts, DBItem i)
        {
            return $"/item/{i.Tag}";
        }

        private static async Task<string> GetBids(string parameter, string name)
        {
            var bidsTask = Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<PlayerBidsCommand.BidResult>.Request,
            List<PlayerBidsCommand.BidResult>>("playerBids", new PaginatedRequestCommand<PlayerBidsCommand.BidResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = parameter });

            var sb = new StringBuilder();
            var bids = await bidsTask;

            sb.Append("<h2>Bids</h2> <ul>");
            foreach (var item in bids)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");

            var auctionAndBids = sb.ToString();
            return auctionAndBids;
        }

        private static async Task<string> GetAuctions(string uuid, string name)
        {
            var auctions = await Server.ExecuteCommandWithCache<
            PaginatedRequestCommand<AuctionResult>.Request,
            List<AuctionResult>>("playerAuctions", new PaginatedRequestCommand<AuctionResult>
            .Request()
            { Amount = 20, Offset = 0, Uuid = uuid });

            var sb = new StringBuilder();

            sb.Append($"<h2>{name} Auctions</h2> <ul>");
            foreach (var item in auctions)
            {
                sb.Append($"<li><a href=\"/auction/{item.AuctionId}\">{item.ItemName}</a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static async Task<string> GetRecentAuctions(string tag)
        {
            if (tag == null)
                return "";
            var isBazaar = ItemPrices.Instance.IsBazaar(ItemDetails.Instance.GetItemIdForTag(tag));
            if (isBazaar)
                return " This is a bazaar item. Bazaartracker.com currently gives you a more detailed view of this history. ";
            var result = await Server.ExecuteCommandWithCache<ItemSearchQuery, IEnumerable<AuctionPreview>>("recentAuctions", new ItemSearchQuery()
            {
                name = tag,
                Start = DateTime.Now.Subtract(TimeSpan.FromHours(3)).RoundDown(TimeSpan.FromMinutes(30))
            });
            var sb = new StringBuilder(200);
            sb.Append("<br>Recent auctions: <ul>");
            foreach (var item in result)
            {
                sb.Append($"<li><a href=\"/auction/{item.Uuid}\">auction by {await PlayerSearch.Instance.GetNameWithCacheAsync(item.Seller)}</a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string AddAlternativeNames(DBItem i)
        {
            if (i.Names == null || i.Names.Count == 0)
                return "";
            return ". Found this item with the following names: " + i.Names.Select(n => n.Name).Aggregate((a, b) => $"{a}, {b}").TrimEnd(' ', ',')
            + ". This are all names under wich we found auctins for this item in the ah. It may be historical names or names in a different language.";
        }

        private static string BottomText(string title, string description)
        {
            return $@"<h1>{title}</h1><p>{description}</p>"
                    + PopularPages();
        }

        private static string PopularPages()
        {
            var r = new Random();
            var recentSearches = SearchService.Instance.GetPopularSites().OrderBy(x => r.Next());
            var body = "<h2>Description</h2><p>View, search, browse, and filter by reforge or enchantment. "
                    + "You can find all current and historic prices for the auction house and bazaar on this web tracker. "
                    + "We are tracking about 250 million auctions. "
                    + "Saved more than 300 million bazaar prices in intervalls of 10 seconds. "
                    + "Furthermore there are over two million <a href=\"/players\"> skyblock players</a> that you can search by name and browse through the auctions they made over the past two years. "
                    + "The autocomplete search is ranked by popularity and allows you to find whatever <a href=\"/items\">item</a> you want faster. "
                    + "New Items are added automatically and available within two miniutes after the first auction is startet. "
                    + "We allow you to subscribe to auctions, item prices and being outbid with more to come. "
                    + "Quick urls allow you to link to specific sites. /p/Steve or /i/Oak allow you to create a link without visiting the site first. "
                    + "Please use the contact on the Feedback site to send us suggestions or bug reports. </p>";
            if (recentSearches.Any())
                body += "<h2>Other Players and item auctions:</h2>"
                    + recentSearches
                    .Take(8)
                .Select(p => $"<a href=\"https://sky.coflnet.com/{p.Url}\">{p.Title} </a>")
                .Aggregate((a, b) => a + b);
            return body + "</noscript>";
        }
    }

    internal class AuctionPreviewParams
    {
        public string Tag { get; }
        public string AuctioneerId { get; }
        public string ItemName { get; }
        public DateTime End { get; }
        public int BidCount { get; }
        public Tier Tier { get; }
        public Category Category { get; }
        public bool Bin { get; }
        public long HighestBidAmount { get; }
        public long UId { get; }

        public AuctionPreviewParams(string tag, string auctioneerId, string itemName, DateTime end, int bidCount, Tier tier, Category category, bool bin, long highestBidAmount, long uId)
        {
            Tag = tag;
            AuctioneerId = auctioneerId;
            ItemName = itemName;
            End = end;
            BidCount = bidCount;
            Tier = tier;
            Category = category;
            Bin = bin;
            HighestBidAmount = highestBidAmount;
            UId = uId;
        }

        public override bool Equals(object obj)
        {
            return obj is AuctionPreviewParams other &&
                   Tag == other.Tag &&
                   AuctioneerId == other.AuctioneerId &&
                   ItemName == other.ItemName &&
                   End == other.End &&
                   BidCount == other.BidCount &&
                   Tier == other.Tier &&
                   Category == other.Category &&
                   Bin == other.Bin &&
                   HighestBidAmount == other.HighestBidAmount;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(Tag);
            hash.Add(AuctioneerId);
            hash.Add(ItemName);
            hash.Add(End);
            hash.Add(BidCount);
            hash.Add(Tier);
            hash.Add(Category);
            hash.Add(Bin);
            hash.Add(HighestBidAmount);
            return hash.ToHashCode();
        }
    }
}
