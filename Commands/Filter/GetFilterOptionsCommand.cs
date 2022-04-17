using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// List options for filters
    /// </summary>
    public class GetFilterOptionsCommand : Command
    {
        static FilterEngine fe = new FilterEngine();
        public override Task Execute(MessageData data)
        {
            var filter = fe.GetFilter(data.GetAs<string>());
            return data.SendBack(data.Create("filterOptions", new FilterOptions(filter), A_HOUR * 2));
        }
    }
}
