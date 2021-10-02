
using System;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Newtonsoft.Json;
using Prometheus;

namespace hypixel
{
    public class SetGoogleIdCommand : Command
    {
        Counter loginCount = Metrics.CreateCounter("loginCount", "How often the login was executed (with a googleid)");
        public override async Task Execute(MessageData data)
        {
            var token = ValidateToken(data.GetAs<string>());

            GoogleUser user;
            try
            {
                user = UserService.Instance.GetOrCreateUser(token.Subject, token.Email);
            }
            catch (Exception e)
            {
                throw new CoflnetException("disabled","For first time login visit sky.coflnet.com/flipper");
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

        public static GoogleJsonWebSignature.Payload ValidateToken(string token)
        {
            try
            {
                var client = GoogleJsonWebSignature.ValidateAsync(token);
                client.Wait();
                var tokenData = client.Result;
                Console.WriteLine("google user: " + tokenData.Name);
                return tokenData;
            }
            catch (Exception e)
            {
                throw new CoflnetException("invalid_token", $"{e.InnerException.Message}");
            }


        }
    }
}