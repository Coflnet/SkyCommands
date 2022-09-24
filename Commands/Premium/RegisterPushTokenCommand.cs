using System.Threading.Tasks;
using MessagePack;
using RestSharp;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class RegisterPushTokenCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();
            var request = new RestRequest("Subscription/{userId}/device", Method.Put)
                    .AddJsonBody(new Device() { Name = args.deviceName, Token = args.token })
                    .AddUrlSegment("userId", data.UserId);
            var response = SubscribeClient.Client.ExecuteAsync(request);
            return Task.CompletedTask;
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("name")]
            public string deviceName;
            [Key("token")]
            public string token;
        }
    }
}