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
            var bad = socket.LastSent.Where(s => s.Uuid == uuid).FirstOrDefault();
            span.Span.Log(JSON.Stringify(bad));
            span.Span.Log(JSON.Stringify(bad.Context));


            if (rating == "down")
            {
                Blacklist(socket, bad);
                socket.SendMessage(new ChatPart(COFLNET + "Thanks for your feedback, Please help us better understand why this flip is bad\n", null, "you can also send free text with /cofl report"),
                    new ChatPart(" * Its overpriced\n", "/cofl report overpriced "),
                    new ChatPart(" * This item sells slowly\n", "/cofl report slow sell"),
                    new ChatPart(" * I blacklisted this before\n", "/cofl report blacklist broken"),
                    new ChatPart(" * Reference auctions are wrong \n", "/cofl report reference auctions are wrong ", "please send /cofl report with further information"));
            }
            else if (rating == "up")
            {
                socket.SendMessage(new ChatPart(COFLNET + "Thanks for your feedback, Please help us better understand why this flip is good\n"),
                                    new ChatPart(" * it isn't I mis-clicked \n", "/cofl report overpriced "),
                                    new ChatPart(" * This item sells fast\n", "/cofl report slow sell"),
                                    new ChatPart(" * High profit\n", "/cofl report slow sell"),
                                    new ChatPart(" * Something else \n", null, "please send /cofl report with further information"));
            }
            else
            {
                socket.SendMessage(COFLNET + $"Thanks for your feedback, you voted this flip " + rating, "/cofl undo", "We will try to improve the flips accordingly");
            }
            await Task.Delay(1000);
            var based = await Server.ExecuteCommandWithCache<string, IEnumerable<BasedOnCommand.Response>>("flipBased", uuid);
            span.Span.Log(string.Join('\n', based.Select(b => $"{b.ItemName} {b.highestBid} {b.uuid}")));
        }

        private static void Blacklist(MinecraftSocket socket, FlipInstance bad)
        {
            if (socket.Settings.BlackList == null)
                socket.Settings.BlackList = new System.Collections.Generic.List<Filter.ListEntry>();
            socket.Settings.BlackList.Add(new Filter.ListEntry() { ItemTag = bad.Tag });
        }
    }


}