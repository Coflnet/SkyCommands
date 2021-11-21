using System;
using System.Threading.Tasks;

namespace hypixel
{
    /// <summary>
    /// Authenticates a given connection to the current user
    /// </summary>
    public class AuthorizeConnectionCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            if (con.LastSettingsChange == null)
            {
                var settings = await CacheService.Instance.GetFromRedis<SettingsChange>("uflipset" + data.UserId);
                if (settings != null)
                    con.LastSettingsChange = settings;
            }
            var lastSettings = con.LastSettingsChange;
            lastSettings.ConIds.Add(data.GetAs<string>());

            lastSettings.UserId = data.UserId;
            if (data.User.HasPremium)
            {
                lastSettings.Tier = AccountTier.PREMIUM;
                lastSettings.ExpiresAt = data.User.PremiumExpires;
            }
            await FlipperService.Instance.UpdateSettings(lastSettings);
            await data.Ok();
        }
    }
}