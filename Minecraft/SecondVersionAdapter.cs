using System;
using System.Linq;
using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public class SecondVersionAdapter : IModVersionAdapter
    {
        MinecraftSocket socket;

        public SecondVersionAdapter(MinecraftSocket socket)
        {
            this.socket = socket;
        }

        public bool SendFlip(FlipInstance flip)
        {
            var message = socket.GetFlipMsg(flip);
            var openCommand = "/viewauction " + flip.Uuid;
            SendMessage(new ChatPart(message, openCommand, string.Join('\n', flip.Interesting.Select(s => "・" + s)) + "\n" + flip.SellerName),
                new ChatPart("[?]", "/cofl reference " + flip.Uuid, "Get reference auctions"),
                new ChatPart(" -", "/cofl blacklist " + flip.Tag, "Blacklist this item type \n(make sure not to open the website)"),
                new ChatPart(" ", openCommand, null));

            if (socket.Settings.ModSettings?.PlaySoundOnFlip ?? false && flip.Profit > 1_000_000)
                SendSound("note.pling", (float)(1 / (Math.Sqrt((float)flip.Profit / 1_000_000) + 1)));
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