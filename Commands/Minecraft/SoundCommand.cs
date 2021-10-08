using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Coflnet.Sky.Commands.MC
{
    public class SoundCommand : McCommand
    {
        public override Task Execute(MinecraftSocket socket, string arguments)
        {
            socket.SendSound(JsonConvert.DeserializeObject<string>(arguments));
            return Task.CompletedTask;
        }
    }
}