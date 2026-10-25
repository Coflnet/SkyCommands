using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Subscribes the current websocket connection to live updates for a single topic.
    /// Sending this again replaces the previous subscription (one topic per connection).
    /// Supported topics:
    ///  - <c>auction/{uuid}</c>: pushes the full auction state (type <c>auctionUpdate</c>) on every new bid.
    ///  - <c>sold/{tag}</c>: pushes sold auctions (type <c>soldAuction</c>) of that item tag matching the optional filter.
    ///  - <c>active/{tag[,tag...]}</c>: pushes new matching active auctions (type <c>activeAuction</c>) and removals (type <c>auctionGone</c>).
    /// </summary>
    public class SubscribeUpdatesCommand : Command
    {
        public override bool Cacheable => false;

        public override async Task Execute(MessageData data)
        {
            if (data is not SocketMessageData socketData)
                throw new CoflnetException("invalid_protocol", "This is a socket command");
            var request = data.GetAs<SubscribeUpdateRequest>();
            if (request == null || string.IsNullOrWhiteSpace(request.Topic))
                throw new CoflnetException("invalid_request", "A `topic` is required to subscribe to updates");
            data.GetService<LiveUpdateService>().Subscribe(socketData.Connection, data.mId, request.Topic, request.Filter, request.KnownUuids);
            await data.Ok();
        }

        [DataContract]
        public class SubscribeUpdateRequest
        {
            [DataMember(Name = "topic")]
            public string Topic { get; set; }
            [DataMember(Name = "filter")]
            public Dictionary<string, string> Filter { get; set; }
            /// <summary>Auction uuids the page already shows (<c>active/</c> topics only, at most 200 are used)</summary>
            [DataMember(Name = "knownUuids")]
            public string[] KnownUuids { get; set; }
        }
    }
}
