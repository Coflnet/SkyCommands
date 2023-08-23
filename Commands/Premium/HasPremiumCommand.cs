using System;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    public class PremiumExpirationCommand : Command
    {

        public override async Task Execute(MessageData data)
        {
            try
            {
                try
                {
                    DateTime until = await DiHandler.ServiceProvider.GetService<PremiumService>().ExpiresWhen(data.UserId);
                    if (until > DateTime.Now)
                    {
                        await data.SendBack(data.Create("premiumExpiration", until));
                        return;
                    }
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "checking premium");
                }
                using (var context = new HypixelContext())
                {
                    var user = data.User;
                    if (user.PremiumExpires > DateTime.Now)
                    {
                        await data.SendBack(data.Create("premiumExpiration", user?.PremiumExpires));
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // no premium
            }
            await data.SendBack(data.Create<string>("premiumExpiration", null));
        }
    }
}
