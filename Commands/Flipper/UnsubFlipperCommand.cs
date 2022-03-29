using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class UnsubFlipperCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            FlipperService.Instance.RemoveConnection(con);
            return data.Ok();
        }
    }
}