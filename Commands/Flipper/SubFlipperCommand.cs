using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.Core;
using Newtonsoft.Json;

namespace Coflnet.Sky.Commands
{
    public class SubFlipperCommand : Command
    {
        public override bool Cacheable => false;
        public override async Task Execute(MessageData data)
        {
            var con = (data as SocketMessageData).Connection;
            await data.SendBack(data.Create("debug", $"Validating"));
            var settings = GetSettings(data);
            await data.SendBack(data.Create("debug", $"Validated successfully"));
            try
            {
                if (settings != null)
                {
                    con.OldFallbackSettings?.CancelCompilation();
                    con.OldFallbackSettings = settings;
                }
                con.SubFlipMsgId = (int)data.mId;
                var userId = data.UserId;
                var updateTask = UpdateSettings(data, settings, userId);

                var lastSettings = con.LatestSettings;

                var expires = await DiHandler.ServiceProvider.GetService<PremiumService>().GetCurrentTier(data.UserId.ToString());
                switch (expires.Item1)
                {
                    case AccountTier.STARTER_PREMIUM:
                        data.GetService<FlipperService>().AddStarterConnection(con);
                        break;
                    case AccountTier.PREMIUM:
                        data.GetService<FlipperService>().AddConnection(con);
                        break;
                    case AccountTier.PREMIUM_PLUS:
                        data.GetService<FlipperService>().AddConnectionPlus(con);
                        break;
                    default:
                        data.GetService<FlipperService>().AddNonConnection(con);
                        break;
                }
                await data.SendBack(data.Create("debug", $"Subbed on " + System.Net.Dns.GetHostName()));

                // load settings
                await updateTask;
                lastSettings.Settings = con.Settings;
                lastSettings.UserId = userId;
                if (lastSettings?.Settings?.AllowedFinders == LowPricedAuction.FinderType.UNKOWN)
                    lastSettings.Settings.AllowedFinders = LowPricedAuction.FinderType.FLIPPER;

                var accountUpdateTask = UpdateAccountInfo(data, expires);
                await data.Ok();
                await accountUpdateTask;
                return;
            }
            catch (CoflnetException e)
            {
                data.GetService<FlipperService>().AddNonConnection(con);
                await data.SendBack(data.Create("debug", $"exception " + e));
            }
            catch (Exception e)
            {
                data.GetService<FlipperService>().AddNonConnection(con);
                dev.Logger.Instance.Error(e, "flip error");
                await data.SendBack(data.Create("debug", $"unkown exception "));
                throw new Exception("unkown exception on subFlip", e);
            }
            // not logged no settings, tell the frontend (request its settings)
            if (settings == null)
                await data.SendBack(data.Create<string>("flipSettings", null));
            await data.Ok();
            await Task.Delay(500); // backof attempt
        }

        public static async Task UpdateAccountInfo(MessageData data, (AccountTier, DateTime) expires)
        {
            var service = DiHandler.ServiceProvider.GetRequiredService<SettingsService>();
            var con = (data as SocketMessageData).Connection;

            try
            {
                if (con.AccountInfo.Value == default)
                    con.AccountInfo = await SelfUpdatingValue<AccountInfo>.Create(data.UserId.ToString(), "accountInfo", () => new AccountInfo()
                    {
                        UserId = data.UserId.ToString(),
                    });


                con.AccountInfo.Value.Tier = expires.Item1;
                con.AccountInfo.Value.ExpiresAt = expires.Item2;
                await con.AccountInfo.Update();
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
                {
                    if (con.Settings != null)
                        FlipFilter.CopyRelevantToNew(settings, con.Settings);
                    await service.UpdateSetting(userId.ToString(), "flipSettings", settings);
                }
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
            await GetFlipSettingsCommand.AssignSettings(con);
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


        protected FlipSettings GetSettings(MessageData data)
        {
            FlipSettings settings = new FlipSettings();
            var testFlip = new FlipInstance()
            {
                Auction = new Core.SaveAuction()
                {
                    ItemName = "test",
                    Tag = "STICK",
                    Bin = true,
                    StartingBid = 2,
                    NBTLookup = new NBTLookup[0],
                    FlatenedNBT = new(),
                    Enchantments = new(),
                    Context = new()
                },
                Finder = LowPricedAuction.FinderType.SNIPER,
                MedianPrice = 100000000,
                LowestBin = 100000,
                Context = new()
            };
            try
            {
                settings = data.GetAs<FlipSettings>();
                if (settings == null)
                    return null; // special case to load settings
                                 // test if settings compile
                settings.MatchesSettings(testFlip);
            }
            catch (CoflnetException)
            {
                throw;
            }
            catch (Exception e)
            {
                // could not get it continue with default
                data.LogError(e, "subFlip");
                CheckListValidity(testFlip, settings.BlackList, data);
                CheckListValidity(testFlip, settings.WhiteList, data, true);
            }
            return settings;
        }

        private void CheckListValidity(FlipInstance testFlip, List<ListEntry> blacklist, MessageData data, bool whiteList = false)
        {
            foreach (var item in blacklist.ToList())
            {
                try
                {
                    var expression = item.GetExpression();
                    expression.Compile()(testFlip);
                }
                catch (Exception e)
                {
                    var jsonWithoutDefault = JsonConvert.SerializeObject(item, Formatting.Indented, new JsonSerializerSettings()
                    {
                        DefaultValueHandling = DefaultValueHandling.Ignore
                    });
                    data.SendBack(data.Create("error", $"The following list entry could not be loaded please fix or remove it: {jsonWithoutDefault}"));
                    data.SendBack(data.Create("debug", $"Error: {e.Message}"));
                }
            }
        }
    }
}