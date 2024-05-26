using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Payments.Client.Api;

namespace Coflnet.Sky.Commands
{
    public class TransferCoinsCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var userApi = data.GetService<UserApi>();
            var args = data.GetAs<TransferRequest>();
            var userCheck = CanUserSend(data);
            string targetUser;
            if (!string.IsNullOrEmpty(args.TargetUserEmail))
                targetUser = (await UserService.Instance.GetUserIdByEmail(args.TargetUserEmail)).ToString();
            else if (!string.IsNullOrEmpty(args.TargetUserMc) && args.TargetUserMc.Length == 32)
            {
                var userInfo = await data.GetService<McAccountService>().GetUserId(args.TargetUserMc);
                if (userInfo == null)
                    throw new CoflnetException("not_found", "That user doesn't have any verified accounts. Please tell them to connect their Minecraft account to their Coflnet account or ask them for their email");
                targetUser = userInfo.ExternalId;
            }
            else
                throw new CoflnetException("missing_argument", "Either `email` or `mcId` have to be passed to know where to send funds to");
            if (targetUser == "0")
                throw new CoflnetException("not_found", "There was no user found with this identifier");
            if (!await userCheck)
                throw new CoflnetException("payment_error", "You need to verify with a unique Minecraft account to send funds to other users (at most one email per account)");
            try
            {
                var transaction = await userApi.UserUserIdTransferPostAsync(data.UserId.ToString(), new Coflnet.Payments.Client.Model.TransferRequest()
                {
                    Amount = args.Amount,
                    Reference = (args.TargetUserEmail + string.Empty + args.TargetUserMc) + args.Reference.Truncate(5),
                    TargetUser = targetUser
                });
                await data.SendBack(data.Create("success", args.Amount));
            }
            catch (Payments.Client.Client.ApiException ex)
            {
                throw new CoflnetException("payment_error", ex.Message.Substring("Error calling UserUserIdTransferPost: {.Message.:".Length));
            }
        }

        private async Task<bool> CanUserSend(MessageData data)
        {
            var transactions = await data.GetService<ITransactionApi>().TransactionUUserIdGetAsync(data.UserId.ToString(), 0, 50);
            return transactions.Count > 10 || transactions.Any(t => t.ProductId == "verify_mc");
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