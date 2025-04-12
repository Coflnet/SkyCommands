using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Newtonsoft.Json;

namespace Coflnet.Sky.Commands;

public class UpdateConfigCommand : SelfDocumentingCommand<ConfigUpdateArgs, Void>
{
    protected override async Task<Void> Execute(TypedMessageData data)
    {
        using var createdConfigs = await SelfUpdatingValue<CreatedConfigs>.Create(data.UserId.ToString(), "created_configs", () => new());
        var publishAs = data.Get().ConfigName;
        if (!createdConfigs.Value.Configs.Contains(publishAs))
        {
            throw new CoflnetException("config_not_found", "The config you are trying to update does not exist");
        }
        var settings = JsonConvert.DeserializeObject<FlipSettings>(JsonConvert.SerializeObject(data.Connection.Settings));
        RemoveBaseConfig(settings.WhiteList);
        RemoveBaseConfig(settings.BlackList);
        var key = GetKeyFromname(publishAs);
        using var current = await SelfUpdatingValue<ConfigContainer>.Create(data.UserId.ToString(), key, () => throw new CoflnetException("config_not_found", "The config you are trying to update does not exist"));
        var diff = SettingsDiffer.GetDifferences(current.Value.Settings ?? throw new CoflnetException("load_issue", "couldn't load current settings, try again or report"), settings);
        if (diff.GetDiffCount() == 0)
            throw new CoflnetException("no_changes", "No changes found in the config, aborting update");
        var currentBlCount = current.Value.Settings.BlackList.Count();
        if (currentBlCount > settings.BlackList.Count * 2)
            throw new CoflnetException("blacklist_too_large", $"More than half of the blacklisted items were removed ({currentBlCount}-{settings.BlackList.Count()}), aborting update");
        var newVersion = current.Value.Version + 1;
        if (current.Value.Diffs.ContainsKey(newVersion))
            throw new CoflnetException("version_already_exists", "Config already/very recently updated with a new version. Please wait a few seconds and try again");
        current.Value.Settings = settings;
        current.Value.ChangeNotes = data.Get().ChangeNotes;
        current.Value.Version = newVersion;
        current.Value.Settings.UsedVersion = current.Value.Version;
        current.Value.Diffs.Add(newVersion, diff);
        current.Value.LastUpdated = DateTime.UtcNow;
        if (current.Value.Settings.PublishedAs != null && current.Value.Settings.PublishedAs != publishAs)
        {
            throw new CoflnetException("config_name_mismatch", $"You are trying to update {publishAs} but the config was previously published as {current.Value.Settings.PublishedAs}. Please edit the config name as file to switch.");
        }
        if (current.Value.Settings.PublishedAs == null)
        {
            current.Value.Settings.PublishedAs = publishAs;
        }
        if (current.Value.Diffs.Count > 20)
        {
            current.Value.Diffs.Remove(current.Value.Diffs.Keys.Min());
        }
        await current.Update();
        await DiHandler.GetService<SettingsService>().UpdateSetting(data.UserId.ToString() + "_archive", key + $"_version_{newVersion}", current.Value);
        await data.SendBack(data.Create("display", new MessageDisplay { Message = $"Config updated to version {newVersion}", Type = MessageDisplay.Success }));
        return null;
    }

    private static void RemoveDupplicates(List<ListEntry> list)
    {
        var dupplicates = list.ToList()
                        .GroupBy(x => x.ItemTag + (x.filter == null ? "" : string.Join(',', x.filter.Select(f => f.ToString()))))
                        .Where(g => g.Count() > 1).SelectMany(g => g.Skip(1));
        foreach (var item in dupplicates)
        {
            list.Remove(item);
            Console.WriteLine("Removed dupplicate");
        }
    }

    private void RemoveBaseConfig(List<ListEntry> whiteList)
    {
        RemoveDupplicates(whiteList);
        foreach (var item in whiteList.ToList())
        {
            if (item.Tags?.Contains("from BaseConfig") ?? false)
            {
                whiteList.Remove(item);
            }
        }
    }

    public static string GetKeyFromname(string name)
    {
        return "seller_config_" + name.ToLower().Truncate(20);
    }
}

[DataContract]
public class MessageDisplay
{
    [DataMember(Name = "message")]
    public string Message { get; set; }
    [DataMember(Name = "type")]
    public string Type { get; set; }

    public static string Success = "success";
    public static string Warning = "warning";
    public static string Error = "error";
}
