using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class GetFlipSettingsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            await AssignSettings(con);
            await data.SendBack(data.Create("flipSettings", con.FlipSettings.Value));
        }

        public static async Task AssignSettings(SkyblockBackEnd con)
        {
            var userId = con.UserId;
            con.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(userId.ToString(), "flipSettings", () => new FlipSettings()
            {
                MinProfit = 100000,
                MinVolume = 2,
                ModSettings = new ModSettings() { ShortNumbers = true },
                Visibility = new VisibilitySettings() { SellerOpenButton = true, ExtraInfoMax = 3, Lore = true }
            });
            con.FlipSettings.Value.PlayerInfo = con;
        }
    }
}