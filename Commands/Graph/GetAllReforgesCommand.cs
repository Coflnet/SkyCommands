using System;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetAllReforgesCommand : Command {
        public override Task Execute (MessageData data) {
            var values = Enum.GetValues (typeof (ItemReferences.Reforge))
                    .Cast<ItemReferences.Reforge>()
                    .Select (ench => ench.ToString ())
                    .ToList ();

            return data.SendBack (new MessageData ("getReforgesResponse",
                JsonConvert.SerializeObject (values),
                A_DAY
            ));
        }
    }
}