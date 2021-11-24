using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;

namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            try
            {
                con.SubFlipMsgId = (int)data.mId;
                SetSettingsOnConnection(data, con);

                var lastSettings = con.LastSettingsChange;


                if (!data.User.HasPremium)
                    FlipperService.Instance.AddNonConnection(con);
                else
                {
                    FlipperService.Instance.AddConnection(con);

                    lastSettings.Tier = AccountTier.PREMIUM;
                    lastSettings.ExpiresAt = data.User.PremiumExpires;
                }

                if (MessagePack.MessagePackSerializer.Serialize(con.Settings).SequenceEqual(MessagePack.MessagePackSerializer.Serialize(lastSettings.Settings)))
                    return; // nothing actually changed
                    
                lastSettings.Settings = con.Settings;
                lastSettings.UserId = data.UserId;
                if(lastSettings.Settings.AllowedFinders == Coflnet.Sky.LowPricedAuction.FinderType.UNKOWN)
                    lastSettings.Settings.AllowedFinders = Coflnet.Sky.LowPricedAuction.FinderType.FLIPPER | Coflnet.Sky.LowPricedAuction.FinderType.SNIPER | Coflnet.Sky.LowPricedAuction.FinderType.SNIPER_MEDIAN;

                await FlipperService.Instance.UpdateSettings(lastSettings);
            }
            catch (CoflnetException e)
            {
                FlipperService.Instance.AddNonConnection(con);
            }
            await data.Ok();
        }

        private static void SetSettingsOnConnection(MessageData data, SkyblockBackEnd con)
        {
            try
            {
                con.Settings = data.GetAs<FlipSettings>();
                if (con.Settings == null)
                    con.Settings = new FlipSettings();
            }
            catch (Exception)
            {
                // could not get it continue with default
                con.Settings = new FlipSettings();
            }
        }
    }
}