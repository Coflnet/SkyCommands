using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    public class SubEventsCommand : Command
    {
        public override bool Cacheable => false;
        public override Task Execute(MessageData data)
        {
            if (!(data is SocketMessageData socketData))
                throw new CoflnetException("invalid_protocol", "This is a socket command");
            var broker = DiHandler.ServiceProvider.GetRequiredService<EventBrokerClient>();
            var id = data.mId;
            Console.WriteLine($"{data.UserId} subbed to events");
            var sub = broker.SubEvents(data.UserId.ToString(), ev =>
            {
                Console.WriteLine($"{data.UserId} receives event {ev.SourceType} {ev.Message}");
                try
                {
                    var response = data.Create("event", ev);
                    response.mId = id;
                    data.SendBack(response);
                }
                catch (Exception e)
                {
                    Console.WriteLine("error on event " + e);
                }
            });
            socketData.Connection.OnBeforeClose += (socket) =>
            {
                sub.Unsubscribe();
            };
            return Task.CompletedTask;
        }
    }
}
