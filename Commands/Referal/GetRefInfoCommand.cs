using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetRefInfoCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var refInfo = ReferalService.Instance.GetReferalInfo(data.User);
            return data.SendBack(data.Create("refInfo", refInfo));
        }
    }
}
