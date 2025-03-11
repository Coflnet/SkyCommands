using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class SubFlipAnonymousCommand : SubFlipperCommand
{
    public override async Task Execute(MessageData data)
    {
        var con = (data as SocketMessageData).Connection;
        con.OldFallbackSettings?.CancelCompilation();
        var settings = GetSettings(data);
        CapListLength(settings.WhiteList, con);
        CapListLength(settings.BlackList, con);
        con.OldFallbackSettings = settings;
        con.SubFlipMsgId = (int)data.mId;
        data.GetService<FlipperService>().AddNonConnection(con);
        await data.Ok();
        await Task.Delay(500); // backof attempt

        static void CapListLength(List<ListEntry> list, SkyblockBackEnd con)
        {
            try
            {
                var id = con.UserId.ToString();
                return; // actually logged in, don't cap
            }
            catch (System.Exception)
            {
                // not logged in, cap
                while (list.Count > 50)
                {
                    list.RemoveAt(0);
                }
            }

        }
    }
}