using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class DeleteAllSubscriptionsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var userId = data.UserId;

                var request = new RestRequest("Subscription/{userId}/sub/all", Method.Delete)
                    .AddUrlSegment("userId", userId);
                var response = await SubscribeClient.Client.ExecuteAsync(request);

                await data.SendBack(new MessageData("unsubscribed", response.Content));
            }
        }
    }
}