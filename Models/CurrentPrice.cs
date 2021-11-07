using System.Runtime.Serialization;

namespace hypixel
{
    [DataContract]
    public class CurrentPrice
    {
        [DataMember(Name = "sell")]
        public double Sell;
        [DataMember(Name = "buy")]
        public double Buy;
        [DataMember(Name = "available")]
        public int Available;
        [DataMember(Name = "updatedAt")]
        public System.DateTime Updated = System.DateTime.Now;
    }
}
