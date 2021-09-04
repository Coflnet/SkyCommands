
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using hypixel;

namespace Coflnet.Sky.Filter
{

    [DataContract]
    public class FlipSettings
    {
        [DataMember(Name = "filters")]
        public Dictionary<string, string> Filters;
        [DataMember(Name = "blacklist")]
        public List<ListEntry> BlackList;
        [DataMember(Name = "whitelist")]
        public List<ListEntry> WhiteList;

        [DataMember(Name = "lbin")]
        public bool BasedOnLBin;

        [DataMember(Name = "minProfit")]
        public int MinProfit;

        [DataMember(Name = "minVolume")]
        public int MinVolume;

        

        private FlipFilter filter;
        private List<FlipFilter> blackListFilters;

        /// <summary>
        /// Determines if a flip matches a the <see cref="Filters"/>> of this instance
        /// </summary>
        /// <param name="flip"></param>
        /// <returns>true if it matches</returns>
        public bool MatchesSettings(FlipInstance flip)
        {
            if(flip.MedianPrice - flip.LastKnownCost < MinProfit)
                return false;
            if(flip.Volume < MinVolume)
                return false;
            if (this.Filters == null && BlackList == null)
                return true;
            if (flip.Auction == null)
            {
                System.Console.WriteLine("flip has no auction");
                return false;
            }

            if (WhiteList != null)
                foreach (var item in WhiteList)
                {
                    if (flip.Tag == item.ItemTag && (item.filter != null || item.MatchesSettings(flip)))
                        return true;
                }

            if (BlackList != null)
            {
                foreach (var item in BlackList)
                {
                    if (flip.Tag == item.ItemTag)
                        return false;
                    if (item.filter != null && item.MatchesSettings(flip))
                        return false;
                }
            }

            if (filter == null)
                filter = new FlipFilter(this.Filters);
            return filter.IsMatch(flip);
        }
    }

    [DataContract]
    public class ListEntry
    {
        [DataMember(Name = "tag")]
        public string ItemTag;
        [DataMember(Name = "filter")]
        public Dictionary<string, string> filter;

        private FlipFilter filterCache;

        public bool MatchesSettings(FlipInstance flip)
        {
            if (filterCache == null)
                filterCache = new FlipFilter(this.filter);
            return filterCache.IsMatch(flip);
        }
    }

    public class FlipFilter
    {
        private static FilterEngine FilterEngine = new FilterEngine();

        private Dictionary<string, string> Filters;

        public FlipFilter(Dictionary<string, string> filters)
        {
            Filters = filters;
        }

        public bool IsMatch(FlipInstance flip)
        {
            return FilterEngine.AddFilters(new SaveAuction[] { flip.Auction }.AsQueryable(), Filters).Any();
        }
    }
}