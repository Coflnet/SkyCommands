using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public class RateCommand : McCommand
    {
        public override async Task Execute(MinecraftSocket socket, string arguments)
        {
            var args = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(arguments).Split(" ");
            var uuid = args[0];
            var finder = args[1];
            var rating = args[2];
            using var span = socket.tracer.BuildSpan("vote").WithTag("type",rating).WithTag("finder",finder).WithTag("uuid",uuid).AsChildOf(socket.ConSpan).StartActive();
            var based = await Server.ExecuteCommandWithCache<string, IEnumerable<BasedOnCommand.Response>>("flipBased", uuid);
            span.Span.Log(JSON.Stringify(socket.LastSent.Where(s=>s.Uuid == uuid).FirstOrDefault()));
            span.Span.Log(string.Join('\n',based.Select(b=>$"{b.ItemName} {b.highestBid} {b.uuid}")));
            socket.SendMessage(COFLNET + $"Thanks for your feedback, you voted this flip " + rating);
        }
    }

    
}