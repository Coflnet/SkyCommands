using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;

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
                throw new CoflnetException("missing_key", "available options are:\n" + string.Join(",\n", updater.Options()));
            var value = arguments.Value.Replace('$', '§').Replace('�', '§');
            var socket = (data as SocketMessageData).Connection;

            var lazyLock = Locks.GetOrAdd(data.UserId, id => new SemaphoreSlim(1));
            try
            {
                await lazyLock.WaitAsync();
                if (socket.FlipSettings == null)
                    await GetFlipSettingsCommand.AssignSettings(socket);
                if (socket.FlipSettings.Value == null)
                    await data.SendBack(data.Create<string>("flipSettings", null));
                await updater.Update(socket, arguments.Key, value);
                socket.Settings.Changer = arguments.Changer;
                var settings = socket.FlipSettings.Value;
                settings.PlayerInfo = socket;
                TestSettings(settings, data.UserId);
                settings.LastChanged = arguments.Key;
                await service.UpdateSetting(data.UserId.ToString(), "flipSettings", socket.Settings);
            }
            finally
            {
                lazyLock.Release();
            }
        }

        private static void TestSettings(FlipSettings settings, int userId)
        {
            try
            {
                settings.MatchesSettings(GetTestFlip("test"));
            }
            catch (CoflnetException)
            {
                throw;
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"validating settings for {userId}\n" + JsonConvert.SerializeObject(settings, Formatting.Indented));
                throw new CoflnetException("invalid_settings", "Your settings are invalid, please revert your last change");
            }
        }

        public static FlipInstance GetTestFlip(string tag)
        {
            return new FlipInstance()
            {
                Auction = new SaveAuction()
                {
                    ItemName = "test",
                    Tag = tag ?? "test",
                    Bin = true,
                    StartingBid = 2,
                    NBTLookup = Array.Empty<NBTLookup>(),
                    FlatenedNBT = new(),
                    Enchantments = new(),
                    Context = new()
                },
                Finder = LowPricedAuction.FinderType.SNIPER,
                MedianPrice = 100000000,
                LowestBin = 100000,
                Context = new()
            };
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