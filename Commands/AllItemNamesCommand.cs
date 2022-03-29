using System.Threading.Tasks;
using Newtonsoft.Json;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class AllItemNamesCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            return data.SendBack(new MessageData("itemNamesResponse",JsonConvert.SerializeObject(ItemDetails.Instance.AllItemNames()),A_WEEK));
        }
    }
}
