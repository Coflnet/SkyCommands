using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class SubFlipperCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            var settings = GetSettings(data);
            try
            {
                if (settings != null)
                    con.OldFallbackSettings = settings;
                con.SubFlipMsgId = (int)data.mId;
                var userId = data.UserId;
                var updateTask = UpdateSettings(data, settings, userId);

                var lastSettings = con.LatestSettings;

                var expires = await DiHandler.ServiceProvider.GetService<PremiumService>().ExpiresWhen(data.UserId);
                if (expires < DateTime.UtcNow)
                    FlipperService.Instance.AddNonConnection(con);
                else
                {
                    FlipperService.Instance.AddConnection(con);
                }

                // load settings
                await updateTask;
                lastSettings.Settings = con.Settings;
                lastSettings.UserId = userId;
                if (lastSettings?.Settings?.AllowedFinders == LowPricedAuction.FinderType.UNKOWN)
                    lastSettings.Settings.AllowedFinders = LowPricedAuction.FinderType.FLIPPER;

                var accountUpdateTask = UpdateAccountInfo(data, expires);
                await data.Ok();
                await accountUpdateTask;

                if (MessagePack.MessagePackSerializer.Serialize(con.Settings).SequenceEqual(MessagePack.MessagePackSerializer.Serialize(lastSettings.Settings)))
                    return; // nothing actually changed

                await FlipperService.Instance.UpdateSettings(lastSettings);
                return;
            }
            catch (CoflnetException)
            {
                FlipperService.Instance.AddNonConnection(con);
            }
            // not logged no settings, tell the frontend (request its settings)
            if (settings == null)
                await data.SendBack(data.Create<string>("flipSettings", null));
            else
                await data.Ok();
        }

        public static async Task UpdateAccountInfo(MessageData data, DateTime expires)
        {
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            var con = (data as SocketMessageData).Connection;

            try
            {
                if (con.AccountInfo.Value == default)
                    con.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(data.UserId.ToString(), "accountInfo", () => new AccountInfo()
                    {
                        UserId = data.UserId,
                    });

                if (expires > DateTime.Now)
                {
                    con.AccountInfo.Value.Tier = AccountTier.PREMIUM;
                    con.AccountInfo.Value.ExpiresAt = expires;
                    await con.AccountInfo.Update();
                }

            }
            catch (Exception e)
            {
                data.LogError(e, "updating accountInfo");
            }
        }

        public static async Task UpdateSettings(MessageData data, FlipSettings settings, int userId)
        {
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            var con = (data as SocketMessageData).Connection;
            try
            {
                if (settings != null)
                    await service.UpdateSetting(userId.ToString(), "flipSettings", settings);
                if (con.FlipSettings?.Value == default)
                {
                    await SubscribeToUpdates(data, userId, con);
                }

                if (settings == null)
                    await data.SendBack(data.Create("flipSettings", con.FlipSettings.Value));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                con.OldFallbackSettings = settings;
            }
        }

        private static async Task SubscribeToUpdates(MessageData data, int userId, SkyblockBackEnd con)
        {
            con.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(userId.ToString(), "flipSettings");
            con.FlipSettings.OnChange += (newsettings) =>
            {
                SendBackUpdates(data, con, newsettings);
            };
        }

        private static void SendBackUpdates(MessageData data, SkyblockBackEnd con, FlipSettings newsettings)
        {
            // send the new settings to the frontend
            var update = data.Create("settingsUpdate", newsettings);
            update.mId = con.SubFlipMsgId;
            data.SendBack(update, false);
        }

        private static AccountInfo SettingsToAccountInfo(SettingsChange lastSettings)
        {
            return new AccountInfo()
            {
                ConIds = lastSettings.ConIds,
                ExpiresAt = lastSettings.ExpiresAt,
                McIds = lastSettings.McIds,
                Tier = lastSettings.Tier,
                UserId = lastSettings.UserId
            };
        }

        protected FlipSettings GetSettings(MessageData data)
        {
            FlipSettings settings = new FlipSettings();
            try
            {
                settings = data.GetAs<FlipSettings>();
                if (settings == null)
                    return null; // special case to load settings
                                 // test if settings compile
                settings.MatchesSettings(new FlipInstance()
                {
                    Auction = new SaveAuction()
                    {
                        Enchantments = new System.Collections.Generic.List<Enchantment>(),
                        FlatenedNBT = new System.Collections.Generic.Dictionary<string, string>(),
                        NBTLookup = new System.Collections.Generic.List<NBTLookup>(),
                        StartingBid = 2
                    }
                });
            }
            catch (CoflnetException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                // could not get it continue with default
                data.LogError(e, "subFlip");
                throw new CoflnetException("invalid_settings", "Your settings are invalid, please revert your last change");
            }
            return settings;

        }
    }
}