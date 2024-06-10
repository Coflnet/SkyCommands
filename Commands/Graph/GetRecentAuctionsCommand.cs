using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class GetRecentAuctionsCommand : Command
{
    public override Task Execute(MessageData data)
    {
        return data.SendBack(data.Create("deprecated", "this command got replaced by the api https://sky.coflnet.com/api", A_MINUTE * 2000));
    }
}
