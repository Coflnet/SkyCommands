using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public class ReferenceCommand : McCommand
    {
        public override async Task Execute(MinecraftSocket socket, string arguments)
        {
            Console.WriteLine(arguments);
            var uuid = arguments.Trim('"');
            socket.ModAdapter.SendMessage(new ChatPart("Caclulating references", "https://sky.coflnet.com/auction/" + uuid, "please give it a second"));
            var based = await Server.ExecuteCommandWithCache<string, IEnumerable<BasedOnCommand.Response>>("flipBased", uuid);
            Console.WriteLine("got based ");
            Console.WriteLine(based.First().uuid);
            socket.ModAdapter.SendMessage(based
                .Select(b => new ChatPart(
                    $"\n-> {b.ItemName} for {MinecraftSocket.FormatPrice(b.highestBid)} {b.end}",
                    "https://sky.coflnet.com/auction/" + b.uuid,
                    "Click to open this auction"))
                .ToArray());
            await Task.Delay(200);
            socket.ModAdapter.SendMessage(new ChatPart(MinecraftSocket.COFLNET + "click this to open the auction on the website (in case you want to report an error or share it)", "https://sky.coflnet.com/auction/" + uuid, "please give it a second"));
        }
    }
}