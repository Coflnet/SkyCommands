using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SubFlipAnonymousCommand : SubFlipperCommand
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            var settings = GetSettings(data);
            con.OldFallbackSettings = settings;
            con.SubFlipMsgId = (int)data.mId;
            FlipperService.Instance.AddNonConnection(con);
            await data.Ok();
        }
    }
}