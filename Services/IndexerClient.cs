using System.Threading.Tasks;
using RestSharp;

namespace hypixel
{
    public class IndexerClient
    {
        public static RestClient Client = new RestClient("http://" + SimplerConfig.SConfig.Instance["INDEXER_HOST"]);
        public static Task<RestSharp.IRestResponse<hypixel.Player>> TriggerNameUpdate(string uuid)
        {
            return Client.ExecuteAsync<Player>(new RestRequest("player/{uuid}", Method.PATCH).AddUrlSegment("uuid", uuid));
        }
    }
}