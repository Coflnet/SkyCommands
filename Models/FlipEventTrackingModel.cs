
using System.Collections.Generic;
using System.Linq;
using hypixel;

namespace Coflnet.Sky.Filter
{
    public class FlipEventTrackingModel
    {
        [Key("playerUUID")]
        public string playerUUID;
        [Key("auctionUUID")]
        public int auctionUUID;
        [Key("event")]
        public FlipTrackerEvent flipTrackerEvent;
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

    public enum FlipTrackerEvent
    {
        PURCHASE_START = 1,
        PURCHASE_CONFIRM = 2,
        FLIP_RECEIVE = 4,
        FLIP_CLICK = 8,
        AUCTION_SOLD = 16
    }
}