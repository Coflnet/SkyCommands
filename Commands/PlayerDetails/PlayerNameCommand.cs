using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class PlayerNameCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var respone = CreateResponse(data, data.GetAs<string>());
            await data.SendBack(await respone);
        }

        public static async Task<MessageData> CreateResponse(MessageData data, string uuid)
        {
            if(uuid.Length < 32)
                return data.Create("nameResponse", uuid, 60);
            var name = await PlayerSearch.Instance.GetName(uuid);
            // player names don't change often, but are easy to compute
            return data.Create("nameResponse", name, 0);
        }
    }
}