using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SetMyReferalCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            throw new CoflnetException("deprecated", "The referral system is now handled by the api");
        }
    }
}
