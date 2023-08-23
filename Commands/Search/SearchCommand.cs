using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SearchCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9_]");
            var search = rgx.Replace(data.Data, "").ToLower();

            var players = PlayerSearch.Instance.Search(search, 5);
            return data.SendBack(data.Create("searchResponse", players, A_WEEK));
        }
    }
}