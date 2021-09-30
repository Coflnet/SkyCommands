using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class GetSubscriptionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var userId = data.UserId;
                
                var subs = (await SubscribeClient.GetSubscriptions(userId)).subscriptions;
                await data.SendBack(data.Create("subscriptions",subs));
            }
        }
    }
}