using System.Linq;
using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public class ThirdVersionAdapter : IModVersionAdapter
    {
        MinecraftSocket socket;

        public ThirdVersionAdapter(MinecraftSocket socket)
        {
            this.socket = socket;
        }

        public bool SendFlip(FlipInstance flip)
        {
            var message = socket.GetFlipMsg(flip);
            var openCommand = "/viewauction " + flip.Uuid;
            socket.Send(Response.Create("flip", new
            {
                messages = new ChatPart[]{
                new ChatPart(message, openCommand, string.Join('\n', flip.Interesting.Select(s => "・" + s)) + "\n" + flip.SellerName),
                new ChatPart("?", "/cofl reference " + flip.Uuid, "Get reference auctions"),
                new ChatPart(" ", openCommand, null)},
                id = flip.Uuid,
                worth = flip.Profit,
                cost = flip.LastKnownCost,
                sound = (string)null
            }));
            return true;
        }

        public void SendMessage(params ChatPart[] parts)
        {
            socket.Send(Response.Create("chatMessage", parts));
        }

        public void SendSound(string name, float pitch = 1)
        {
            socket.Send(Response.Create("playSound", new { name, pitch }));
        }
    }
}