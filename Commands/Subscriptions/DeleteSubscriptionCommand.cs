using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using RestSharp;

namespace hypixel
{
    public class DeleteSubscriptionCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();
                var userId = data.UserId;

                //var affected = SubscribeEngine.Instance.Unsubscribe(userId, args.Topic,args.Type).Result;
                var request = new RestRequest("Subscription/{userId}/sub", Method.DELETE)
                    .AddJsonBody(new SubscribeItem() { Type = args.Type, TopicId = args.Topic }).AddParameter("userId", userId);
                var response = SubscribeClient.Client.ExecuteAsync(request);

                return data.SendBack(data.Create("unsubscribed", response.Result));
            }
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("userId")]
            public string UserId;
            [Key("topic")]
            public string Topic;
            [Key("type")]
            public SubscribeItem.SubType Type;
        }
    }

    public static class SubscribeClient
    {
        public static RestClient Client = new RestClient(SimplerConfig.Config.Instance["SUBSCRIPTION_HOST"]);

    }
}