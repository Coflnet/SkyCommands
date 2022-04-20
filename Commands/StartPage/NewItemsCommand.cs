using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    public class NewItemsCommand : Command
    {
        public override async Task Execute(MessageData data)
        {

            var items = await Shared.DiHandler.ServiceProvider.GetService<Sky.Items.Client.Api.IItemsApi>()
                .ItemsItemsAhNewGetAsync();
            var result = items.Select(i => new { IconUrl = "https://sky.coflnet.com/static/icon/" + i.Tag, Name = i.Name, Tag = i.Tag })
                .Where(i => i.Name != null)
                .Select(i => new Response() { IconUrl = i.IconUrl, Name = i.Name, Tag = i.Tag })
                .ToList();
            await data.SendBack(data.Create("newItemsResponse", items, A_HOUR));
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "tag")]
            public string Tag;
            [DataMember(Name = "icon")]
            public string IconUrl;
        }
    }
}
