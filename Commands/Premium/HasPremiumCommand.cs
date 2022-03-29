using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class PremiumExpirationCommand : Command
    {
        static string premiumPlanName = SimplerConfig.SConfig.Instance["PRODUCTS:PREMIUM"];
        public override async Task Execute(MessageData data)
        {
            try
            {
                using (var context = new HypixelContext())
                {
                    var user = data.User;
                    if (user.PremiumExpires > DateTime.Now)
                    {
                        await data.SendBack(data.Create("premiumExpiration", user?.PremiumExpires));
                        return;
                    }
                }

                var api = new UserApi();
                var until = await api.UserUserIdOwnsProductSlugUntilGetAsync(data.UserId.ToString(), premiumPlanName);
                if (until > DateTime.Now)
                {
                    await data.SendBack(data.Create("premiumExpiration", until));
                    return;
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
