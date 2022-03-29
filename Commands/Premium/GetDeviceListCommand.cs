using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetDeviceListCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var devices = data.User.Devices;
            return data.SendBack(data.Create("devices", devices));

        }
    }
}