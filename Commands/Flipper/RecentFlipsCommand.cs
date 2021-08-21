using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class RecentFlipsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var flipps = FlipperService.Instance.Flipps.Take(50);
            try {
                if (data.UserId != 0)
                    flipps = FlipperService.Instance.Flipps.Skip(50).Take(50);
                if (data.User.HasPremium)
                    flipps = FlipperService.Instance.Flipps.Reverse().Skip(2).Take(50);
            } catch(CoflnetException)
            {
                // no premium, continue
            }
            return data.SendBack(data.Create("flips",flipps,A_MINUTE));
        }
    }
}