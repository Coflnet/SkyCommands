using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class FullSearchCommand : Command
    {
        private const string Type = "searchResponse";

        public override async Task Execute(MessageData data)
        {
            throw new CoflnetException("deprecated", "This command is deprecated, use the api instead");
        }
    }
}