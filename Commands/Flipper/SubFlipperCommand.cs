using System;
using System.Threading.Tasks;

namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            try
            {
                con.SubFlipMsgId = (int)data.mId;
                if (!data.User.HasPremium)
                    FlipperService.Instance.AddNonConnection(con);
                else
                {
                    Console.WriteLine("new premium con");
                    FlipperService.Instance.AddConnection(con);
                    FlipperService.Instance.RemoveNonConnection(con);
                }
            }
            catch (CoflnetException)
            {
                FlipperService.Instance.AddNonConnection(con);
            }
            return data.Ok();
        }
    }
}