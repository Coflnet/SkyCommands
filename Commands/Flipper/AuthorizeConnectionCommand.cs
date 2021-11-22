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
                else 
                    con.LastSettingsChange = new SettingsChange();
            }
            var lastSettings = con.LastSettingsChange;
            var newId = data.GetAs<string>();
            lastSettings.ConIds.Add(newId);

            lastSettings.UserId = data.UserId;
            if (data.User.HasPremium)
            {
                lastSettings.Tier = AccountTier.PREMIUM;
                lastSettings.ExpiresAt = data.User.PremiumExpires;
            }
            data.Span.SetTag("conId", newId);
            var result = await FlipperService.Instance.UpdateSettings(lastSettings);
            data.Log("status: " + result.Status);
            data.Log("delivered " + result.Offset.Value);
            await data.Ok();
        }
    }
}