using System.Linq;
using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public class BlockedCommand : McCommand
    {
        public override async Task Execute(MinecraftSocket socket, string arguments)
        {
            if (socket.TopBlocked.Count == 0)
            {
                socket.SendMessage(COFLNET + "No blocked flips found, make sure you don't click this shortly after the 'flips in 10 seconds' message. (the list gets reset when that message appears)");
                return;
            }
            socket.SendMessage(socket.TopBlocked.Select(b =>
            {
                socket.Settings.GetPrice(b.Flip, out long targetPrice, out long profit);
                return new ChatPart(
                    $"{b.Flip.Name} (+{socket.FormatPrice(profit)}) got blocked because {b.Reason.Replace("SNIPER", "experimental flip finder")}\n",
                    "https://sky.coflnet.com/auction/" + b.Flip.Uuid,
                    "Click to open");
            })
            .Append(new ChatPart() { text = COFLNET + "These are high profit examples of blocked flips." }).ToArray());
        }
    }
}