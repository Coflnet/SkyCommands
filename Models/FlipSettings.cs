
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using hypixel;
using Newtonsoft.Json;

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

        [DataMember(Name = "minProfitPercent")]
        public int MinProfitPercent;

        [DataMember(Name = "minVolume")]
        public int MinVolume;

        [DataMember(Name = "maxCost")]
        public int MaxCost;

        [DataMember(Name = "visibility")]
        public VisibilitySettings Visibility;

        [DataMember(Name = "mod")]
        public ModSettings ModSettings;


        private FlipFilter filter;
        private List<FlipFilter> blackListFilters;

        /// <summary>
        /// Determines if a flip matches a the <see cref="Filters"/>> of this instance
        /// </summary>
        /// <param name="flip"></param>
        /// <returns>true if it matches</returns>
        public bool MatchesSettings(FlipInstance flip)
        {

            if (flip.MedianPrice - flip.LastKnownCost < MinProfit)
                return false;
            // don't show above lbin if not wanted
            if (BasedOnLBin && flip.LowestBin - flip.LastKnownCost < MinProfit)
                return false;
            if (flip.Volume < MinVolume)
                return false;
            if (MaxCost != 0 && flip.LastKnownCost > MaxCost)
                return false;
            if (flip.LastKnownCost > 0 && (flip.Profit / flip.LastKnownCost) * 100 < MinProfitPercent)
            {
                return false;
            }
            if (flip.Auction == null)
                return false;

            if (WhiteList != null)
                foreach (var item in WhiteList)
                {
                    if (item.ItemTag != null && flip.Tag == item.ItemTag || (item.filter != null && item.MatchesSettings(flip)))
                        return true;
                }
            if (BlackList != null)
            {
                foreach (var item in BlackList)
                {
                    if (flip.Tag != null && flip.Tag == item.ItemTag)
                        return false;
                    if (item.filter != null && item.filter.Count > 0 && item.MatchesSettings(flip))
                        return false;
                }
            }

            if (filter == null)
                filter = new FlipFilter(this.Filters);
            return filter.IsMatch(flip);
        }
    }
}