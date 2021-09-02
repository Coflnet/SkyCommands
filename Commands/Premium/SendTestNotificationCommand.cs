using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class SendTestNotificationCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            throw new CoflnetException("deprecated","this endpoint was deprecated because the frontend didn't use it");
        }
    }
}