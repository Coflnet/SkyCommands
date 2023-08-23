using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class ItemSyncCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var response = context.Items.Include(i => i.Names).ToList();
                return data.SendBack(new MessageData("itemSyncResponse", System.Convert.ToBase64String(MessagePack.MessagePackSerializer.Serialize(response)) ));
            }
        }
    }
}
