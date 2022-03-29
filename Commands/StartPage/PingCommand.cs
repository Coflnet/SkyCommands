using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class PingCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return Task.CompletedTask;
        }
    }
}
