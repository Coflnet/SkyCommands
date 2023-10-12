using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class LoginWithToken : Command
{
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
        var user = UserService.Instance.GetUserByEmail(email);
        data.UserId = user.Id;
        con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo", () => new());
        var internalToken = data.GetService<TokenService>().CreateToken(email);
        await con.SendBack(data.Create("token", internalToken));
    }
}