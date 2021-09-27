using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public abstract class McCommand
    {
        public abstract Task Execute(MinecraftSocket socket, string arguments);
    }
}