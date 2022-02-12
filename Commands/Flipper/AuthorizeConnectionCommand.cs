using System;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace hypixel
{
    /// <summary>
    /// Authenticates a given connection to the current user
    /// </summary>
    public class AuthorizeConnectionCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            if (con.LatestSettings == null)
            {
                var settings = await CacheService.Instance.GetFromRedis<SettingsChange>("uflipset" + data.UserId);
                if (settings != null)
                    con.LatestSettings = settings;
                else
                    con.LatestSettings = new SettingsChange();
            }
            var lastSettings = con.LatestSettings;
            var newId = data.GetAs<string>();
            lastSettings.ConIds.Add(newId);

            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            await service.UpdateSetting("mod", newId, data.UserId.ToString());

            lastSettings.UserId = data.UserId;
            if (data.User.HasPremium)
            {
                lastSettings.Tier = AccountTier.PREMIUM;
                lastSettings.ExpiresAt = data.User.PremiumExpires;
            }
            await SubFlipperCommand.UpdateAccountInfo(data, lastSettings);
            await data.Ok();
            data.Span.SetTag("conId", newId);
            var result = await FlipperService.Instance.UpdateSettings(lastSettings);
            data.Log("status: " + result.Status);
            data.Log("delivered " + result.Offset.Value);
        }
    }
}