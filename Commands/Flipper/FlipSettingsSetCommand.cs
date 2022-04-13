using System;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;
using System.Runtime.Serialization;

namespace Coflnet.Sky.Commands
{
    public class FlipSettingsSetCommand : Command
    {
        private static SettingsUpdater updater = new SettingsUpdater();
        public override async Task Execute(MessageData data)
        {
            var arguments = data.GetAs<Update>();
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            if(string.IsNullOrEmpty(arguments.Key))
                throw new CoflnetException("missing_key", "available options are:\n" + String.Join(",\n", updater.Options()));
            var value = arguments.Value.Replace('$', '§');
            var socket = (data as SocketMessageData).Connection;
            if(socket.FlipSettings == null)
                socket.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(data.UserId.ToString(), "flipSettings");
            await updater.Update(socket, arguments.Key, value);
            socket.Settings.Changer = arguments.Changer;
            data.Log(Newtonsoft.Json.JsonConvert.SerializeObject(socket.Settings,Newtonsoft.Json.Formatting.Indented));
            await service.UpdateSetting(data.UserId.ToString(), "flipSettings", socket.Settings);
        }

        [DataContract]
        public class Update
        {
            [DataMember(Name = "key")]
            public string Key;
            [DataMember(Name = "value")]
            public string Value;
            [DataMember(Name = "changer")]
            public string Changer;
        }
    }
}