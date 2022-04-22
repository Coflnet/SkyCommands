using System;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Threading;

namespace Coflnet.Sky.Commands
{
    public class FlipSettingsSetCommand : Command
    {
        private static SettingsUpdater updater = new SettingsUpdater();
        private ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

        public override async Task Execute(MessageData data)
        {
            var arguments = data.GetAs<Update>();
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            if (string.IsNullOrEmpty(arguments.Key))
                throw new CoflnetException("missing_key", "available options are:\n" + String.Join(",\n", updater.Options()));
            var value = arguments.Value.Replace('$', '§');
            var socket = (data as SocketMessageData).Connection;

            var lazyLock = Locks.GetOrAdd(data.UserId, id => new SemaphoreSlim(1));
            try
            {
                await lazyLock.WaitAsync();
                if (socket.FlipSettings == null)
                    socket.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(data.UserId.ToString(), "flipSettings");
                await updater.Update(socket, arguments.Key, value);
                socket.Settings.Changer = arguments.Changer;
                socket.FlipSettings.Value.MatchesSettings(new FlipInstance()
                {
                    Auction = new SaveAuction()
                    {
                        Enchantments = new System.Collections.Generic.List<Enchantment>(),
                        FlatenedNBT = new System.Collections.Generic.Dictionary<string, string>(),
                        NBTLookup = new System.Collections.Generic.List<NBTLookup>()
                    },
                    LastKnownCost = 1
                });
                data.Log(Newtonsoft.Json.JsonConvert.SerializeObject(socket.Settings, Newtonsoft.Json.Formatting.Indented));
                await service.UpdateSetting(data.UserId.ToString(), "flipSettings", socket.Settings);
            }
            finally
            {
                lazyLock.Release();
            }
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