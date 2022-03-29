using System.Threading.Tasks;
using MessagePack;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class TrackSearchCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var hit = data.GetAs<Request>();
            if(hit.Type=="player" && hit.Id.Length == 32)
                PlayerSearch.Instance.AddHitFor(hit.Id);
            else 
                ItemDetails.Instance.AddHitFor(hit.Id);

            await SearchService.Instance.AddPopularSite(hit.Type,hit.Id);
            await data.Ok();
            
            TrackingService.Instance.TrackPage($"http://sky.coflnet.com/{hit.Type}/{hit.Id}",$"{hit.Type}/{hit.Id}",data);
        }
        [MessagePackObject]
        public class Request
        {
            [Key("type")]
            public string Type;
            [Key("id")]
            public string Id;
        }
    }
}