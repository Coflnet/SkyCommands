using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class TransferCoinsCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var productsApi = new UserApi("http://" + SimplerConfig.Config.Instance["PAYMENTS_HOST"]);
            var args = data.GetAs<TransferRequest>();
            var targetUser = "0";
            if(!string.IsNullOrEmpty(args.TargetUserEmail))
                targetUser = (await UserService.Instance.GetUserIdByEmail(args.TargetUserEmail)).ToString();
            else if(!string.IsNullOrEmpty(args.TargetUserMc) && args.TargetUserMc.Length == 32)
            {
                var userInfo = await McAccountService.Instance.GetUserId(args.TargetUserMc);
                if(userInfo == null)
                    throw new CoflnetException("not_found", "That user doesn't have any verified accounts. Please tell them to connect their Minecraft account to their Coflnet account or ask them for their email");
                targetUser = userInfo.ExternalId;
            }
            else 
                throw new CoflnetException("missing_argument","Either `email` or `mcId` have to be passed to know where to send funds to");
            if(targetUser == "0")
                throw new CoflnetException("not_found","There was no user found with this identifier");
            var transaction = await productsApi.UserUserIdTransferPostAsync(data.UserId.ToString(), new Coflnet.Payments.Client.Model.TransferRequest()
            {
                Amount = args.Amount,
                Reference = (args.TargetUserEmail ?? args.TargetUserMc) + args.Reference.Truncate(5),
                TargetUser = targetUser
            });
            await data.SendBack(data.Create("success", args.Amount));
        }

        [DataContract]
        public class TransferRequest 
        {
            [DataMember(Name = "email")]
            public string TargetUserEmail;
            [DataMember(Name = "mcId")]
            public string TargetUserMc;
            [DataMember(Name = "reference")]
            public string Reference;
            [DataMember(Name = "amount")]
            public double Amount;

        }
    }

}