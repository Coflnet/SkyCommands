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
            McConnect.Models.User mcAccounts = await ExecuteUserRequest(mcRequest);
            return mcAccounts.Accounts.OrderByDescending(a => a.UpdatedAt).Where(a => a.Verified).FirstOrDefault();
        }

        private async Task<McConnect.Models.User> ExecuteUserRequest(IRestRequest mcRequest)
        {
            var mcResponse = await mcAccountClient.ExecuteAsync(mcRequest);
            var mcAccounts = JsonConvert.DeserializeObject<Coflnet.Sky.McConnect.Models.User>(mcResponse.Content);
            return mcAccounts;
        }

        public async Task<ConnectionRequest> ConnectAccount(string userId, string uuid)
        {
            var response = (await mcAccountClient.ExecuteAsync(new RestRequest("connect/user/{userId}", Method.POST)
                                .AddUrlSegment("userId", userId).AddQueryParameter("mcUuid", uuid))).Content;
            return JsonConvert.DeserializeObject<ConnectionRequest>(response);
        }
        public async Task<Coflnet.Sky.McConnect.Models.User> GetUserId(string mcId)
        {
            return await ExecuteUserRequest(new RestRequest("connect/minecraft/{mcId}", Method.POST)
                                .AddUrlSegment("mcId", mcId));

        }


        public class ConnectionRequest
        {
            public int Code { get; set; }
            public bool IsConnected { get; set; }
        }
    }
}