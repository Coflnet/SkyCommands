
using System.Collections.Generic;
using System.Linq;
using hypixel;

namespace Coflnet.Sky.Filter
{
    public class NewFlipTrackingModel
    {
        [Key("auctionUUID")]
        public string auctionUUID;
        [Key("targetPrice")]
        public int targetPrice;
        [Key("finderType")]
        public string finderType;
        [Key("timestamp")]
        public long timestamp
        {
            set
            {
                if (value == 0)
                {
                    End = DateTime.Now;
                }
                else
                    End = value.ThisIsNowATimeStamp();
            }
            get
            {
                return End.ToUnix();
            }
        }
    }
}