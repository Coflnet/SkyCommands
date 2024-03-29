using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class LoginWithToken : Command
{
    public override bool Cacheable => false;
    public override async Task Execute(MessageData data)
    {
        try
        {
            if (data is not SocketMessageData con)
                return;
            await LoginWithSelfToken(data, con);
        }
        catch (System.Exception e)
        {
            dev.Logger.Instance.Error(e, "login with token default");
            await new SetGoogleIdCommand().Execute(data);
        }
    }

    private static async Task LoginWithSelfToken(MessageData data, SocketMessageData con)
    {
        var email = data.GetService<TokenService>().GetEmailFromToken(data.GetAs<string>());
        GoogleUser user = await UserService.Instance.GetUserByEmail(email);
        data.UserId = user.Id;
        con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo", () => new());
        con.Log($"User {user.Id} logged in with token {email}");
        var internalToken = data.GetService<TokenService>().CreateToken(email);
        await con.SendBack(data.Create("token", internalToken));
        await data.SendBack(data.Create("debug", user.Id));
    }
}