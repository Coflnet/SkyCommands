using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SendTestNotificationCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            throw new CoflnetException("deprecated","this endpoint was deprecated because the frontend didn't use it");
        }
    }
}