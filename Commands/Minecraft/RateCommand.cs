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
            using var span = socket.tracer.BuildSpan("vote").WithTag("type", rating).WithTag("finder", finder).WithTag("uuid", uuid).AsChildOf(socket.ConSpan).StartActive();
            var based = await Server.ExecuteCommandWithCache<string, IEnumerable<BasedOnCommand.Response>>("flipBased", uuid);
            var bad = socket.LastSent.Where(s => s.Uuid == uuid).FirstOrDefault();
            span.Span.Log(JSON.Stringify(bad));
            span.Span.Log(string.Join('\n', based.Select(b => $"{b.ItemName} {b.highestBid} {b.uuid}")));
            socket.SendMessage(COFLNET + $"Thanks for your feedback, you voted this flip " + rating, "/cofl undo", "We will try to improve the flips accordingly");
            if (rating == "down")
                Blacklist(socket, bad);
        }

        private static void Blacklist(MinecraftSocket socket, FlipInstance bad)
        {
            if (socket.Settings.BlackList == null)
                socket.Settings.BlackList = new System.Collections.Generic.List<Filter.ListEntry>();
            socket.Settings.BlackList.Add(new Filter.ListEntry() { ItemTag = bad.Tag });
        }
    }


}