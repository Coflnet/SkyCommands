using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Authenticates a given connection to the current user
    /// </summary>
    public class AuthorizeConnectionCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var newId = data.GetAs<string>();
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            var authTask = service.UpdateSetting("mod", newId, data.UserId.ToString());
            await data.Ok();
            // legacy
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
            if (lastSettings.ConIds.Count > 5)
            {
                lastSettings.ConIds.Remove(lastSettings.ConIds.FirstOrDefault());
                lastSettings.ConIds.Remove(lastSettings.ConIds.FirstOrDefault());
            }
            lastSettings.ConIds.Add(newId);


            lastSettings.UserId = data.UserId;
            var expires = await DiHandler.ServiceProvider.GetService<PremiumService>().GetCurrentTier(data.UserId);
            await SubFlipperCommand.UpdateAccountInfo(data, expires);
            await authTask;
        }
    }
}