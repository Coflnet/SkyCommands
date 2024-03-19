using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Subscriptions.Client.Api;

namespace Coflnet.Sky.Commands
{
    public class GetSubscriptionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var userId = data.UserId;

            var subs = (await data.GetService<ISubscriptionApi>().SubscriptionUserIdGetAsync(userId.ToString())).Subscriptions;
            using (var context = new HypixelContext())
            {
                await data.SendBack(data.Create("subscriptions", subs));
            }
        }
    }
}