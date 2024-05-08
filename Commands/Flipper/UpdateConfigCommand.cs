using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;

public class UpdateConfigCommand : SelfDocumentingCommand<ConfigUpdateArgs, Void>
{
    protected override async Task<Void> Execute(TypedMessageData data)
    {
        using var createdConfigs = await SelfUpdatingValue<CreatedConfigs>.Create(data.UserId.ToString(), "created_configs", () => new());
        if (!createdConfigs.Value.Configs.Contains(data.Get().ConfigName))
        {
            throw new CoflnetException("config_not_found", "The config you are trying to update does not exist");
        }
        var settings = data.Connection.Settings;
        var key = GetKeyFromname(data.Get().ConfigName);
        using var current = await SelfUpdatingValue<ConfigContainer>.Create(data.UserId.ToString(), key, () => throw new CoflnetException("config_not_found", "The config you are trying to update does not exist"));
        current.Value.Settings = settings;
        current.Value.ChangeNotes = data.Get().ChangeNotes;
        await current.Update();
        return null;
    }

    public static string GetKeyFromname(string name)
    {
        return "seller_config_" + name.ToLower().Truncate(20);
    }
}
