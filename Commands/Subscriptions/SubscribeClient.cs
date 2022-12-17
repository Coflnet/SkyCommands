using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public static class SubscribeClient
    {
        public static RestClient Client = new RestClient(
                    SimplerConfig.Config.Instance["SUBSCRIPTION_BASE_URL"] 
                    ?? "http://" + SimplerConfig.Config.Instance["SUBSCRIPTION_HOST"]);

        public static async Task<UserResponse> GetSubscriptions(int userId)
        {
            var userSubsRequest = new RestRequest("Subscription/{userId}", Method.Get)
                .AddUrlSegment("userId", userId);

            var subscriptionsResponse = (await SubscribeClient.Client.ExecuteAsync(userSubsRequest)).Content;
            try
            {
                return JsonConvert.DeserializeObject<UserResponse>(subscriptionsResponse);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "loading subscriptions");
                dev.Logger.Instance.Log(subscriptionsResponse);
                throw e;
            }
        }

        public class UserResponse
        {
            public List<SubscribeItem> subscriptions { get; set; }
        }

    }
}