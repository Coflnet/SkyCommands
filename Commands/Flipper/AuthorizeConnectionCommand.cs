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
            // verify that id is valid
            var idBytes = Convert.FromBase64String(newId);
            if (idBytes.Length < 16)
                throw new CoflnetException("invalid_id", "The passed connection id is invalid (too short)");
            if (idBytes.Length == 17)
            {
                // check checksum
                var checksum = idBytes[16];
                var sum = 0;
                for (int i = 0; i < 16; i++)
                {
                    sum += idBytes[i];
                }
                if (sum % 256 != checksum)
                    throw new CoflnetException("invalid_id", "The passed connection id is invalid, please get the link from minecraft again");
                if(data is not SocketMessageData socketData)
                    throw new CoflnetException("invalid_id", "This command can not be called via api");
                newId = Convert.ToBase64String(idBytes, 0, 16);
                Console.WriteLine($"New id for {data.UserId}: " + newId);
            }

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
            var expires = await DiHandler.ServiceProvider.GetService<PremiumService>().GetCurrentTier(data.UserId.ToString());
            await SubFlipperCommand.UpdateAccountInfo(data, expires);
            await authTask;
        }
    }
}