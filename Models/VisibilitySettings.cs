using System.Runtime.Serialization;

namespace Coflnet.Sky.Filter
{
    [DataContract]
    public class VisibilitySettings
    {
        [DataMember(Name = "cost")]
        public bool Cost;
        [DataMember(Name = "estProfit")]
        public bool EstimatedProfit;
        [DataMember(Name = "lbin")]
        public bool LowestBin;
        [DataMember(Name = "slbin")]
        public bool SecondLowestBin;
        [DataMember(Name = "medPrice")]
        public bool MedianPrice;
        [DataMember(Name = "seller")]
        public bool Seller;
        [DataMember(Name = "volume")]
        public bool Volume;
        [DataMember(Name = "extraFields")]
        public int ExtraInfoMax;
        [DataMember(Name = "avgSellTime")]
        public bool AvgSellTime;
        [DataMember(Name = "profitPercent")]
        public bool ProfitPercentage;
        [DataMember(Name = "profit")]
        public bool Profit;
    }
}