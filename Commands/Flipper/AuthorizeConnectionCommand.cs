using System.Threading.Tasks;

namespace hypixel
{
    /// <summary>
    /// Authenticates a given connection to the current user
    /// </summary>
    public class AuthorizeConnectionCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            var lastSettings = con.LastSettingsChange;
            lastSettings.ConIds.Add(data.GetAs<long>());
            lastSettings.UserId = data.UserId;
            await FlipperService.Instance.UpdateSettings(lastSettings);
            await data.Ok();
        }
    }
}