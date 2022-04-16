using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    public class PremiumExpirationCommand : Command
    {
        static string premiumPlanName = SimplerConfig.SConfig.Instance["PRODUCTS:PREMIUM"];

        public override async Task Execute(MessageData data)
        {
            try
            {
                try
                {
                    DateTime until = await When(data.UserId);
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

        public static async Task<DateTime> When(int userId)
        {
            if(GoogleUser.EveryoneIsPremium)
                return DateTime.Now + TimeSpan.FromDays(30);
            var api = DiHandler.ServiceProvider.GetService<UserApi>();
            var until = await api.UserUserIdOwnsProductSlugUntilGetAsync(userId.ToString(), premiumPlanName);
            return until;
        }
    }
}
