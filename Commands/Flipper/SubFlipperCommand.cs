using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Filter;
using Microsoft.Extensions.DependencyInjection;

namespace hypixel
{
    public class SubFlipperCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            var settings = GetSettings(data);
            try
            {
                con.SubFlipMsgId = (int)data.mId;
                var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
                var userId = data.UserId;
                try
                {
                    if (settings != null)
                        await service.UpdateSetting(userId.ToString(), "flipSettings", settings);
                    if (con.FlipSettings.Value == default)
                        con.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(userId.ToString(), "flipSettings");
                    if (settings == null)
                        await data.SendBack(data.Create("flipSettings", con.FlipSettings.Value));
                }
                catch (Exception e)
                {
                    data.LogError(e, "updating flipsettings");
                    con.OldFallbackSettings = settings;
                }

                var lastSettings = con.LatestSettings;


                if (!data.User.HasPremium)
                    FlipperService.Instance.AddNonConnection(con);
                else
                {
                    FlipperService.Instance.AddConnection(con);

                    lastSettings.Tier = AccountTier.PREMIUM;
                    lastSettings.ExpiresAt = data.User.PremiumExpires;
                }

                if (MessagePack.MessagePackSerializer.Serialize(con.Settings).SequenceEqual(MessagePack.MessagePackSerializer.Serialize(lastSettings.Settings)))
                    return; // nothing actually changed

                lastSettings.Settings = con.Settings;
                lastSettings.UserId = userId;
                if (lastSettings.Settings.AllowedFinders == Coflnet.Sky.LowPricedAuction.FinderType.UNKOWN)
                    lastSettings.Settings.AllowedFinders = Coflnet.Sky.LowPricedAuction.FinderType.FLIPPER;

                var accountInfo = new AccountInfo()
                {
                    ConIds = lastSettings.ConIds,
                    ExpiresAt = lastSettings.ExpiresAt,
                    McIds = lastSettings.McIds,
                    Tier = lastSettings.Tier,
                    UserId = lastSettings.UserId
                };
                try
                {
                    await service.UpdateSetting(data.UserId.ToString(), "accountInfo", accountInfo);
                    if (con.AccountInfo.Value == default)
                        con.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(data.UserId.ToString(), "accountInfo");
                }
                catch (Exception e)
                {
                    data.LogError(e, "updating accountInfo");
                }

                await FlipperService.Instance.UpdateSettings(lastSettings);
            }
            catch (CoflnetException e)
            {
                FlipperService.Instance.AddNonConnection(con);
            }
            await data.Ok();
        }

        private static FlipSettings GetSettings(MessageData data)
        {
            FlipSettings settings = new FlipSettings();
            try
            {
                settings = data.GetAs<FlipSettings>();
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