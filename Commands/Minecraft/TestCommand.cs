using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public class TestCommand : McCommand
    {
        public override Task Execute(MinecraftSocket socket, string arguments)
        {
            socket.SendSound("minecraft:entity.experience_orb.pickup");
            socket.SendMessage("The test was successful :)");
            return Task.CompletedTask;
        }
    }
}