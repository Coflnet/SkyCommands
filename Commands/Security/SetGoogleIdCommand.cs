
using System;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Newtonsoft.Json;
using Prometheus;
using RestSharp;

namespace hypixel
{
    public class SetGoogleIdCommand : Command
    {
        Counter loginCount = Metrics.CreateCounter("loginCount", "How often the login was executed (with a googleid)");
        RestClient client = new RestClient("http://" + SimplerConfig.SConfig.Instance["INDEXER_HOST"]);
        public override async Task Execute(MessageData data)
        {
            var token = await ValidateToken(data.GetAs<string>());

            GoogleUser user;
            try
            {
                user = UserService.Instance.GetOrCreateUser(token.Subject, token.Email);
            }
            catch (Exception)
            {
                var request = new RestRequest("user", Method.POST).AddJsonBody(new GoogleUser() { GoogleId = token.Subject, Email = token.Email });
                var response = await client.ExecuteAsync<GoogleUser>(request);
                user = response.Data;
                Console.WriteLine("created new user " + user.Id);
            }
            data.UserId = user.Id;
            try
            {
                if ((data is SocketMessageData con))
                {
                    var settings = await CacheService.Instance.GetFromRedis<SettingsChange>("uflipset" + user.Id);
                    if (settings != null)
                        con.Connection.LastSettingsChange = settings;
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "loading flip settings on login");
            }
            loginCount.Inc();
            await data.Ok();
        }

        public static async Task<GoogleJsonWebSignature.Payload> ValidateToken(string token)
        {
            try
            {
                var tokenData = await GoogleJsonWebSignature.ValidateAsync(token);
                Console.WriteLine("google user: " + tokenData?.Name);
                return tokenData;
            }
            catch (Exception e)
            {
                throw new CoflnetException("invalid_token", $"{e?.InnerException?.Message}");
            }


        }
    }
}