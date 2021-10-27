using System;
using System.Threading.Tasks;
using RestSharp;

namespace Coflnet.Sky.Commands.MC
{
    public class NextUpdateRetriever
    {
        static RestClient client = new RestClient("http://" + SimplerConfig.SConfig.Instance["UPDATER_HOST"]);
        public async Task<DateTime> Get()
        {
            try
            {
                var last = await client.ExecuteAsync<DateTime>(new RestRequest("/api/time"));
                return last.Data + TimeSpan.FromSeconds(61);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "getting next update time");
                throw e;
            }
        }
    }
}