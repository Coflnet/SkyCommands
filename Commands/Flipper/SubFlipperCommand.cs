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

                if (!data.User.HasPremium)
                    FlipperService.Instance.AddNonConnection(con, (int)data.mId);
                else
                {
                    FlipperService.Instance.AddConnection(con, (int)data.mId);
                    FlipperService.Instance.RemoveNonConnection(con);
                }
            }
            catch (CoflnetException)
            {
                FlipperService.Instance.AddNonConnection(con, (int)data.mId);
            }
            return data.Ok();
        }
    }
}