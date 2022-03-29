using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
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