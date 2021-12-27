
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
        public override bool Cacheable => false;
        Counter loginCount = Metrics.CreateCounter("loginCount", "How often the login was executed (with a googleid)");
        public override async Task Execute(MessageData data)
        {
            GoogleJsonWebSignature.Payload token;
            try
            {
                token = await ValidateToken(data.GetAs<string>());
            }
            catch (Exception e)
            {
                data.LogError(e, "login error");
                throw new CoflnetException("invalid_token", $"{e?.InnerException?.Message}");
            }

            GoogleUser user;
            try
            {
                user = UserService.Instance.GetOrCreateUser(token.Subject, token.Email);
            }
            catch (Exception)
            {
                var request = new RestRequest("user", Method.POST).AddJsonBody(new GoogleUser() { GoogleId = token.Subject, Email = token.Email });
                var response = await IndexerClient.Client.ExecuteAsync<GoogleUser>(request);
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
                        con.Connection.LatestSettings = settings;
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
            var tokenData = await GoogleJsonWebSignature.ValidateAsync(token);
            return tokenData;
        }
    }
}