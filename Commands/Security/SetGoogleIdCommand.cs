using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using Google.Apis.Auth;
using Prometheus;

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
                token = await ValidateToken(NormalizeToken(data.GetAs<string>()));
            }
            catch (Exception e)
            {
                data.LogError(e, "login error");
                throw new CoflnetException("invalid_token", e?.InnerException?.Message ?? e?.Message);
            }

            var user = await GetOrCreateUser(token);
            data.UserId = user.Id;
            try
            {
                if (data is not SocketMessageData con)
                    return;
                con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo", () => new());
                var accountInfo = con.Connection.AccountInfo;
                var email = user.Email ?? token.Email;
                if (string.IsNullOrWhiteSpace(email))
                    throw new CoflnetException("missing_email", "The Google token did not include an email address");
                var internalToken = data.GetService<TokenService>().CreateToken(email);
                await con.SendBack(data.Create("token", internalToken));
                Console.WriteLine($"User {user.Id} logged in");
                if (string.IsNullOrEmpty(accountInfo.Value.Locale))
                {
                    accountInfo.Value.Locale = token.Locale;
                    await accountInfo.Update();
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "loading flip settings on login");
                await data.Ok();
            }
            loginCount.Inc();
        }

        public static async Task<GoogleJsonWebSignature.Payload> ValidateToken(string token)
        {
            var tokenData = await GoogleJsonWebSignature.ValidateAsync(token);
            return tokenData;
        }

        private static async Task<GoogleUser> GetOrCreateUser(GoogleJsonWebSignature.Payload token)
        {
            try
            {
                var user = UserService.Instance.GetOrCreateUser(token.Subject, token.Email);
                if (user != null)
                    return user;
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "failed to get or create google user locally");
            }

            var existingUser = await TryFindExistingUser(token);
            if (existingUser != null)
                return EnsureUserEmail(existingUser, token);

            var request = new RestRequest("user", Method.Post).AddJsonBody(new GoogleUser() { GoogleId = token.Subject, Email = token.Email });
            var response = await IndexerClient.Client.ExecuteAsync<GoogleUser>(request);
            if (response?.Data != null)
            {
                Console.WriteLine("created new user " + response.Data.Id);
                return EnsureUserEmail(response.Data, token);
            }

            existingUser = await TryFindExistingUser(token);
            if (existingUser != null)
                return EnsureUserEmail(existingUser, token);

            throw new CoflnetException("user_creation_failed", response?.ErrorMessage ?? response?.StatusDescription ?? response?.StatusCode.ToString() ?? "Could not load or create a user for this Google account");
        }

        private static async Task<GoogleUser> TryFindExistingUser(GoogleJsonWebSignature.Payload token)
        {
            try
            {
                var user = UserService.Instance.GetUser(token.Subject);
                if (user != null)
                    return user;
            }
            catch (Exception)
            {
            }

            if (string.IsNullOrWhiteSpace(token.Email))
                return null;

            try
            {
                return await UserService.Instance.GetUserByEmail(token.Email);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static GoogleUser EnsureUserEmail(GoogleUser user, GoogleJsonWebSignature.Payload token)
        {
            if (string.IsNullOrWhiteSpace(token.Email) || string.Equals(user.Email, token.Email, StringComparison.OrdinalIgnoreCase))
                return user;

            try
            {
                var updatedUser = UserService.Instance.GetOrCreateUser(token.Subject, token.Email);
                if (updatedUser != null)
                    return updatedUser;
            }
            catch (Exception)
            {
            }

            user.Email = token.Email;
            return user;
        }

        private static string NormalizeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return token;
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return token["Bearer ".Length..].Trim();
            return token.Trim();
        }
    }
}