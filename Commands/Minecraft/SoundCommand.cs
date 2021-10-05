using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public class SoundCommand : McCommand
    {
        public override Task Execute(MinecraftSocket socket, string arguments)
        {
            socket.SendSound(arguments);
            return Task.CompletedTask;
        }
    }
}