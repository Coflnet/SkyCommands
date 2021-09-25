using System;
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

                var lastSettings = con.LastSettingsChange;
                lastSettings.Settings = con.Settings;
                lastSettings.UserId = data.UserId;
                if (!data.User.HasPremium)
                    FlipperService.Instance.AddNonConnection(con);
                else
                {
                    Console.WriteLine("new premium con");
                    FlipperService.Instance.AddConnection(con);

                    lastSettings.Tier = AccountTier.PREMIUM;
                    lastSettings.ExpiresAt = data.User.PremiumExpires;
                }
                await FlipperService.Instance.UpdateSettings(lastSettings);
            }
            catch (CoflnetException)
            {
                FlipperService.Instance.AddNonConnection(con);
            }
            await data.Ok();
        }
    }
}