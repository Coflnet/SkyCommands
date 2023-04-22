using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class RecentFlipsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var flipps = data.GetService<FlipperService>().Flipps.Take(50);
            try
            {
                if (data.UserId != 0)
                    flipps = data.GetService<FlipperService>().Flipps.Skip(50).Take(50);
                if (data.User.HasPremium)
                    flipps = data.GetService<FlipperService>().Flipps.Reverse().Skip(2).Take(50);
            }
            catch (CoflnetException)
            {
                // no premium, continue
            }
            return data.SendBack(data.Create("flips", flipps, A_MINUTE));
        }
    }
}