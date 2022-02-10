using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using RestSharp;

namespace hypixel
{
    public class DeleteSubscriptionCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var args = data.GetAs<Arguments>();
                var userId = data.UserId;

                var request = new RestRequest("Subscription/{userId}/sub", Method.DELETE)
                    .AddJsonBody(new SubscribeItem()
                    {
                        Type = args.Type,
                        TopicId = args.Topic,
                        Filter = args.Filter,
                        Price = args.Price
                    })
                    .AddUrlSegment("userId", userId);
                var response = await SubscribeClient.Client.ExecuteAsync(request);

                await data.SendBack(new MessageData("unsubscribed", response.Content));
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
            [Key("filter")]
            public string Filter;
            [Key("price")]
            public long Price;
        }
    }
}