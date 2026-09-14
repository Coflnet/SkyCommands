using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Payments.Client.Api;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Commands
{
    public class TransferCoinsCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            data.Log("Attempted to use removed TransferCoinsCommand. " + data.UserId);
            throw new CoflnetException("removed", "This command has been removed and is no longer available.");

        }
    }

}