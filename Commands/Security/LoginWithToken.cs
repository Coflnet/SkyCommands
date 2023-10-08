using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;
public class LoginWithToken : Command
{
    public override async Task Execute(MessageData data)
    {
        var email = data.GetService<TokenService>().GetEmailFromToken(data.GetAs<string>());
        var user = UserService.Instance.GetUserByEmail(email);
        data.UserId = user.Id;
        if (data is not SocketMessageData con)
            return;
        con.Connection.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(user.Id.ToString(), "accountInfo", () => new());
    }
}