using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands;
public class GetPublishedConfigsCommand : SelfDocumentingCommand<string, string[]>
{
    protected override async Task<string[]> Execute(TypedMessageData data)
    {
        using var current = await SelfUpdatingValue<CreatedConfigs>.Create(data.UserId.ToString(), "created_configs", () => new());
        return current.Value.Configs.ToArray();
    }
}

[DataContract]
public class ConfigUpdateArgs
{
    [DataMember(Name = "configName")]
    public string ConfigName;
    [DataMember(Name = "config")]
    public string ChangeNotes;
}
public class Void
{
}