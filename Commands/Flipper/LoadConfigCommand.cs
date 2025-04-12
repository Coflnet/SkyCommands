using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using MessagePack;

namespace Coflnet.Sky.Commands;

public class LoadConfigCommand : SelfDocumentingCommand<ConfigLoadArgs, Void>
{
    protected override async Task<Void> Execute(TypedMessageData data)
    {
        var key = UpdateConfigCommand.GetKeyFromname(data.Get().ConfigName);
        var config = (await SelfUpdatingValue<ConfigContainer>.Create(data.UserId.ToString(), key, () => throw new CoflnetException("config_not_found", "The config you are trying to load does not exist"))).Value;
        await data.Connection.FlipSettings.Update(config.Settings);
        return null;
    }
}

[MessagePackObject]
public class ConfigLoadArgs
{
    [Key("configName")]
    public string ConfigName { get; set; }
}