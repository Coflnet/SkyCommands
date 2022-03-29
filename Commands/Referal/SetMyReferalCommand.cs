using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SetMyReferalCommand : Command
    {
        public override bool Cacheable => false;
        public override Task Execute(MessageData data)
        {
            ReferalService.Instance.WasReferedBy(data.User, data.GetAs<string>());
            return data.Ok();
        }
    }
}
