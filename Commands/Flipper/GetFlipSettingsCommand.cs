using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace hypixel
{
    public class GetFlipSettingsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            con.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(data.UserId.ToString(), "flipSettings");
            await data.SendBack(data.Create("flipSettings", con.FlipSettings.Value));
        }
    }
}