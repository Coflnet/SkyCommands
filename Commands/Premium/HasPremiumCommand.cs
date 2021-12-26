using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;

namespace hypixel
{
    public class PremiumExpirationCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            try
            {
                using (var context = new HypixelContext())
                {
                    var user = data.User;
                    await data.SendBack(data.Create("premiumExpiration", user?.PremiumExpires));
                    if(user.PremiumExpires > DateTime.Now)
                        return;
                }

                var api = new UserApi();
                var until = await api.UserUserIdOwnsProductSlugUntilGetAsync(data.UserId.ToString(), "premium-plan");
                if(until > DateTime.Now)
                {
                    await data.SendBack(data.Create("premiumExpiration", until));
                    return;
                }
            }
            catch (Exception)
            {
                // no premium
                await data.SendBack(data.Create<string>("premiumExpiration", null));
            }
        }
    }
}
