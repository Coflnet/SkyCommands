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
        var diff = SettingsDiffer.GetDifferences(current.Value.Settings, settings);
        if (diff.GetDiffCount() == 0)
            throw new CoflnetException("no_changes", "No changes found in the config, aborting update");
        if (diff.BlacklistRemoved.Count > settings.BlackList.Count())
            throw new CoflnetException("blacklist_too_large", "More than half of the blacklisted items were removed, aborting update");
        var newVersion = current.Value.Version + 1;
        if (current.Value.Diffs.ContainsKey(newVersion))
            throw new CoflnetException("version_already_exists", "Config already/very recently updated with a new version. Please wait a few seconds and try again");
        current.Value.Settings = settings;
        current.Value.ChangeNotes = data.Get().ChangeNotes;
        current.Value.Version = newVersion;
        current.Value.Diffs.Add(newVersion, diff);
        await current.Update();
        await DiHandler.GetService<SettingsService>().UpdateSetting(data.UserId.ToString() + "_archive", key + $"_version_{newVersion}", current.Value);
        return null;
    }

    public static string GetKeyFromname(string name)
    {
        return "seller_config_" + name.ToLower().Truncate(20);
    }
}
