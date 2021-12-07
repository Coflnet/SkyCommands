using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;

namespace hypixel
{
    public class PlayerPreviewCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            await data.SendBack(data.Create("preview",
                        await PreviewService.Instance.GetPlayerPreview(data.GetAs<string>()),
                        A_WEEK/2));
        }
    }
}