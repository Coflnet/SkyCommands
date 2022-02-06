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

                try
                {
                    if (settings != null)
                        await service.UpdateSetting(data.UserId.ToString(), "flipSettings", settings);
                    con.FlipSettings = await SelfUpdatingValue<FlipSettings>.Create(data.UserId.ToString(), "flipSettings");
                    if (settings == null)
                        await data.SendBack(data.Create("flipSettings", con.FlipSettings.Value));
                }
                catch (Exception e)
                {
                    data.LogError(e, "updating accountInfo");
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
                lastSettings.UserId = data.UserId;
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