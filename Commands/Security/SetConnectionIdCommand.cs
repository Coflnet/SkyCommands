using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SetConnectionIdCommand : Command
    {
        public override bool Cacheable => false;
        public override Task Execute(MessageData data)
        {
            var socketData = data as SocketMessageData;
            if(socketData == null)
                throw new CoflnetException("invalid_command","this command can only be called by a socket connection");
            socketData.Connection.SetConnectionId(data.GetAs<string>());
            return data.Ok();
        }
    }
}