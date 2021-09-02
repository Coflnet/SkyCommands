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
            var userSubsRequest = new RestRequest("Subscription/{userId}", Method.GET)
                .AddParameter("userId", user.Id);

            var subscriptionsResponse = (await SubscribeClient.Client.ExecuteAsync(userSubsRequest)).Content;
            var subscriptions = JsonConvert.DeserializeObject<UserResponse>(subscriptionsResponse).subscriptions;
            if (!user.HasPremium && subscriptions.Count() >= 3)
                throw new NoPremiumException("Nonpremium users can only have 3 subscriptions");
        
            var request = new RestRequest("Subscription/{userId}/sub", Method.PUT)
                .AddJsonBody(new SubscribeItem() { Type = args.Type, TopicId = args.Topic })
                .AddParameter("userId", user.Id);
            var response = SubscribeClient.Client.ExecuteAsync(request);

            await data.Ok();
        }

        public class UserResponse
        {
            public List<SubscribeItem> subscriptions { get; set; }
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
        }
    }
}