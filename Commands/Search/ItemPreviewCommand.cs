using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;

namespace hypixel
{
    public class ItemPreviewCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            await data.SendBack(data.Create("preview",
                        await PreviewService.Instance.GetItemPreview(data.GetAs<string>()),
                        A_DAY));
        }
    }
}