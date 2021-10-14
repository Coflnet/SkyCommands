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
            socket.ModAdapter.SendMessage(new ChatPart("Caclulating references ",null,"please give it a second"));
            var based = await Server.ExecuteCommandWithCache<string, IEnumerable<BasedOnCommand.Response>>("flipBased", arguments.Trim('"'));
            Console.WriteLine("got based ");
            Console.WriteLine(based.First().uuid);
            socket.ModAdapter.SendMessage(based
                .Select(b => new ChatPart(
                    $"-> {b.ItemName} for {MinecraftSocket.FormatPrice(b.highestBid)} {b.end}",
                    "https://sky.coflnet.com/auction/" + b.uuid, 
                    "Click to open this auction"))
                .ToArray());
        }
    }
}