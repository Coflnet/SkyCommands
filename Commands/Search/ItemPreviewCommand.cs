using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class ItemPreviewCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            await data.SendBack(data.Create("preview",
                        await data.GetService<PreviewService>().GetItemPreview(data.GetAs<string>(), false),
                        A_DAY));
        }
    }
}