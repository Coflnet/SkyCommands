using System.Linq;
using Newtonsoft.Json;
using hypixel;
using RestSharp;
using System.Threading.Tasks;

namespace Coflnet.Sky.Commands
{
    public class McAccountService 
    {
        public static McAccountService Instance = new McAccountService();
        RestClient mcAccountClient = new RestClient("http://" + SimplerConfig.Config.Instance["MCCONNECT_HOST"]);


        public async Task<Coflnet.Sky.McConnect.Models.MinecraftUuid> GetActiveAccount(int userId)
        {
            var mcRequest = new RestRequest("connect/user/{userId}")
                                .AddUrlSegment("userId", userId);
            var mcResponse = await mcAccountClient.ExecuteAsync(mcRequest);
            var mcAccounts  = JsonConvert.DeserializeObject<Coflnet.Sky.McConnect.Models.User>(mcResponse.Content);
            return mcAccounts.Accounts.OrderByDescending(a=>a.UpdatedAt).FirstOrDefault();
        }

        public async Task<string> ConnectAccount(string userId, string uuid)
        {
            return (await mcAccountClient.ExecuteAsync(new RestRequest("connect/user/{userId}", Method.POST)
                                .AddUrlSegment("userId", userId).AddQueryParameter("mcUuid", uuid))).Content;
        }
    }
}