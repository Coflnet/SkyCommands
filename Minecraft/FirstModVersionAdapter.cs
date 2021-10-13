using System.Linq;
using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public class FirstModVersionAdapter : IModVersionAdapter
    {
        MinecraftSocket socket;

        public FirstModVersionAdapter(MinecraftSocket socket)
        {
            this.socket = socket;
        }

        public bool SendFlip(FlipInstance flip)
        {
            socket.SendMessage(socket.GetFlipMsg(flip), "/viewauction " + flip.Uuid, string.Join('\n', flip.Interesting.Select(s => "・" + s)));
            return true;
        }

        public void SendMessage(params ChatPart[] parts)
        {
            var part = parts.FirstOrDefault();
            socket.SendMessage(part.text, part.onClick, part.hover);
        }

        public void SendSound(string name, float pitch = 1f)
        {
            // no support
        }
    }
}