using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class PushSubscribeCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();


            var user = data.User;
            var userId = user.Id;
            List<SubscribeItem> subscriptions = (await SubscribeClient.GetSubscriptions(userId)).subscriptions;

            if (!user.HasPremium && subscriptions.Count() >= 3)
                throw new NoPremiumException("Nonpremium users can only have 3 subscriptions");

            var request = new RestRequest("Subscription/{userId}/sub", Method.POST)
                .AddJsonBody(new SubscribeItem() { Type = args.Type, TopicId = args.Topic, Price = args.Price })
                .AddUrlSegment("userId", user.Id);
            var response = await SubscribeClient.Client.ExecuteAsync(request);
            await data.Ok();
        }


        [MessagePackObject]
        public class Arguments
        {
            [Key("price")]
            public long Price;
            [Key("topic")]
            public string Topic;
            [Key("type")]
            public SubscribeItem.SubType Type;
            [Key("filter")]
            public string Filter;
        }
    }
}