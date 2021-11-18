using System.Threading.Tasks;

namespace Coflnet.Sky.Commands.MC
{
    public class BlacklistCommand : McCommand
    {
        public override Task Execute(MinecraftSocket socket, string arguments)
        {
            var tag = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(arguments);
            if(socket.Settings.BlackList == null)
                socket.Settings.BlackList = new System.Collections.Generic.List<Filter.ListEntry>();
            socket.Settings.BlackList.Add(new Filter.ListEntry(){ItemTag = tag});
            socket.SendMessage(COFLNET + $"You blacklisted all {arguments} from appearing");
            return Task.CompletedTask;
        }
    }
}