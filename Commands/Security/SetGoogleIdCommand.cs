
using System;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Newtonsoft.Json;
using Prometheus;
using RestSharp;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
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
                var request = new RestRequest("user", Method.Post).AddJsonBody(new GoogleUser() { GoogleId = token.Subject, Email = token.Email });
                var response = await IndexerClient.Client.ExecuteAsync<GoogleUser>(request);
                user = response.Data;
                Console.WriteLine("created new user " + user.Id);
            }
            data.UserId = user.Id;
            await data.Ok();
            try
            {
                if ((data is SocketMessageData con))
                {
                    var settings = await CacheService.Instance.GetFromRedis<SettingsChange>("uflipset" + user.Id);
                    if (settings != null)
                        con.Connection.LatestSettings = settings;
                    con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo");
                    var accountInfo = con.Connection.AccountInfo;
                    if(string.IsNullOrEmpty(accountInfo.Value.Locale))
                    {
                        accountInfo.Value.Locale = token.Locale;
                        await accountInfo.Update();
                    }
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "loading flip settings on login");
            }
            loginCount.Inc();
        }

        public static async Task<GoogleJsonWebSignature.Payload> ValidateToken(string token)
        {
            var tokenData = await GoogleJsonWebSignature.ValidateAsync(token);
            return tokenData;
        }
    }
}