using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class LoginWithToken : Command
{
    public override bool Cacheable => false;
    public override async Task Execute(MessageData data)
    {
        if (data is not SocketMessageData con)
            return;

        var token = NormalizeToken(data.GetAs<string>());
        var tokenService = data.GetService<TokenService>();

        if (tokenService.TryGetEmailFromToken(token, out var email))
        {
            await LoginWithSelfToken(data, con, email);
            return;
        }

        await new SetGoogleIdCommand().Execute(data);
    }

    private static async Task LoginWithSelfToken(MessageData data, SocketMessageData con, string email)
    {
        GoogleUser user = await UserService.Instance.GetUserByEmail(email);
        if (user == null)
            throw new CoflnetException("user_not_found", "No user exists for the token email");
        data.UserId = user.Id;
        con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo", () => new());
        con.Log($"User {user.Id} logged in with token {email}");
        var internalToken = data.GetService<TokenService>().CreateToken(email);
        await con.SendBack(data.Create("token", internalToken));
        await data.SendBack(data.Create("debug", user.Id));
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return token;
        if (token.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
            return token["Bearer ".Length..].Trim();
        return token.Trim();
    }
}