using System.Threading.Tasks;

namespace hypixel
{
    public class UnsubFlipperCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            FlipperService.Instance.RemoveNonConnection(con);
            FlipperService.Instance.RemoveConnection(con);
            return data.Ok();
        }
    }
}