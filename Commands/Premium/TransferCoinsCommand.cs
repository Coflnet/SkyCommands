using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Payments.Client.Api;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class TransferCoinsCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var userApi = DiHandler.GetService<UserApi>();
            var args = data.GetAs<TransferRequest>();
            var targetUser = "0";
            if (!string.IsNullOrEmpty(args.TargetUserEmail))
                targetUser = (await UserService.Instance.GetUserIdByEmail(args.TargetUserEmail)).ToString();
            else if (!string.IsNullOrEmpty(args.TargetUserMc) && args.TargetUserMc.Length == 32)
            {
                var userInfo = await McAccountService.Instance.GetUserId(args.TargetUserMc);
                if (userInfo == null)
                    throw new CoflnetException("not_found", "That user doesn't have any verified accounts. Please tell them to connect their Minecraft account to their Coflnet account or ask them for their email");
                targetUser = userInfo.ExternalId;
            }
            else
                throw new CoflnetException("missing_argument", "Either `email` or `mcId` have to be passed to know where to send funds to");
            if (targetUser == "0")
                throw new CoflnetException("not_found", "There was no user found with this identifier");
            try
            {
                var transaction = await userApi.UserUserIdTransferPostAsync(data.UserId.ToString(), new Coflnet.Payments.Client.Model.TransferRequest()
                {
                    Amount = args.Amount,
                    Reference = (args.TargetUserEmail ?? args.TargetUserMc) + args.Reference.Truncate(5),
                    TargetUser = targetUser
                });
                await data.SendBack(data.Create("success", args.Amount));
            }
            catch (Payments.Client.Client.ApiException ex)
            {
                throw new CoflnetException("payment_error", ex.Message.Substring("Error calling UserUserIdTransferPost: {.Message.:".Length));
            }
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