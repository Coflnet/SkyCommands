using System.Threading.Tasks;
using MessagePack;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SocketMessageData : MessageData
    {

        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public SkyblockBackEnd Connection;
        private int responseCounter = 0;

        [IgnoreMember]
        public override int UserId
        {
            get => Connection.UserId;
            set => Connection.UserId = value;
        }

        public SocketMessageData()
        {
        }

        public override Task SendBack(MessageData data, bool cache = true)
        {
            if (data.mId == 0)
                data.mId = mId;
            if (cache)
                CacheService.Instance.Save(this, data, responseCounter++);
            Connection.SendBack(data);
            Span.SetTag("result", data.Type);
            return Task.CompletedTask;
        }
    }
}
