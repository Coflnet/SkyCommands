using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetVersionCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(data.Create("version",Program.Version,A_DAY));
        }
    }
}
