using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.PlayerName.Client.Api;

namespace Coflnet.Sky.Commands
{
    public class AccountInfoCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var user = data.User;
            var mcName = "unknow";

            var activeAccount = await data.GetService<McAccountService>().GetActiveAccount(user.Id);
                    
            if (activeAccount != null && activeAccount.AccountUuid != null)
                mcName = (await DiHandler.GetService<IPlayerNameApi>()
                    .PlayerNameNameUuidGetAsync(activeAccount.AccountUuid)).Trim('"');
            await data.SendBack(data.Create("acInfo", new Response(user.Email, activeAccount?.AccountUuid, mcName)));
        }

        [DataContract]
        public class Response
        {
            [DataMember(Name = "email")]
            public string Email;
            [DataMember(Name = "mcId")]
            public string MinecraftId;
            [DataMember(Name = "mcName")]
            public string MinecraftName;

            public Response()
            {
            }

            public Response(string email, string minecraftId, string mcName)
            {
                Email = email;
                MinecraftId = minecraftId;
                MinecraftName = mcName;
            }

            
        }
    }
}
