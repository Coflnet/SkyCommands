using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public class BlacklistCommand : McCommand
    {
        public override async Task Execute(MinecraftSocket socket, string arguments)
        {
            var tag = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(arguments);
            await socket.UpdateSettings(settings =>
            {
                if (settings.Settings.BlackList == null)
                    settings.Settings.BlackList = new System.Collections.Generic.List<Filter.ListEntry>();
                settings.Settings.BlackList.Add(new Filter.ListEntry() { ItemTag = tag });
                return settings;
            });
            socket.SendMessage(COFLNET + $"You blacklisted all {arguments} from appearing");
        }
    }
}