using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using RestSharp;

namespace hypixel
{
    public class DeleteAllSubscriptionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var userId = data.UserId;

                //var affected = SubscribeEngine.Instance.Unsubscribe(userId, args.Topic,args.Type).Result;
                var request = new RestRequest("Subscription/{userId}/sub/all", Method.DELETE)
                    .AddUrlSegment("userId", userId);
                var response = await SubscribeClient.Client.ExecuteAsync(request);

                await data.SendBack(new MessageData("unsubscribed", response.Content));
            }
        }
    }
}