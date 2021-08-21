using System.Threading.Tasks;
using MessagePack;

namespace hypixel
{
    public class RegisterPushTokenCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();
            throw new CoflnetException("deactivated","subscriptions are currently unavailable. If you want them back tell us on the discord");
            //NotificationService.Instance.AddToken(data.UserId, args.deviceName, args.token);
            return Task.CompletedTask;
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("name")]
            public string deviceName;
            [Key("token")]
            public string token;
        }
    }
}