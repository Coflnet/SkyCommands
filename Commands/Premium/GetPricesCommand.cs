using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetPricesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            throw new CoflnetException("obsolete", "Purchases are handled via the api now");
        }
    }
}