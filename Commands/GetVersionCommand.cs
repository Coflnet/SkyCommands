using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetVersionCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(data.Create("version", "0.4.1-dragon", A_DAY));
        }
    }
}
