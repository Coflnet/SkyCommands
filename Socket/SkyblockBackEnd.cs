using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MessagePack;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RateLimiter;
using ComposableAsync;
using Coflnet.Sky.Commands;
using Coflnet.Sky.Filter;
using Coflnet.Sky;
using Coflnet.Sky.Commands.Shared;

namespace hypixel
{
    public class SkyblockBackEnd : WebSocketBehavior, IFlipConnection
    {
        public static Dictionary<string, Command> Commands = new Dictionary<string, Command>();
        private static ConcurrentDictionary<long, SkyblockBackEnd> Subscribers = new ConcurrentDictionary<long, SkyblockBackEnd>();
        public static int ConnectionCount => Subscribers.Count;

        public long Id { get; set; }
        public int SubFlipMsgId;

        private static Prometheus.Counter FlipSendCount = Prometheus.Metrics.CreateCounter("sky_commands_flip_send", "How many flips were sent ");

        private int _userId;
        /// <summary>
        /// if user logged in this is set to his id. throws an exception otherwise
        /// </summary>
        public int UserId
        {
            get
            {
                if (_userId == 0)
                    throw new CoflnetException("user_not_set", "please login first before executing this");
                return _userId;
            }
            set
            {
                _userId = value;
            }
        }

        public FlipSettings Settings { get; internal set; }

        /// <summary>
        /// The last or default settings change captured for this user/connection
        /// </summary>
        /// <returns></returns>
        public SettingsChange LatestSettings { get; set;} = new SettingsChange();

        private TimeLimiter limiter;
        public static event Action NextUpdateStart;

        static SkyblockBackEnd()
        {
            Commands.Add("search", new SearchCommand());
            Commands.Add("itemPrices", new ItemPricesCommand());
            Commands.Add("playerDetails", new PlayerDetailsCommand());
            Commands.Add("version", new GetVersionCommand());
            Commands.Add("auctionDetails", new AuctionDetails());
            Commands.Add("itemDetails", new ItemDetailsCommand());
            Commands.Add("clearCache", new ClearCacheCommand());
            Commands.Add("playerAuctions", new PlayerAuctionsCommand());
            Commands.Add("playerBids", new PlayerBidsCommand());
            Commands.Add("allItemNames", new AllItemNamesCommand());
            Commands.Add("getAllEnchantments", new GetAllEnchantmentsCommand());
            Commands.Add("getEnchantments", new GetEnchantmentsCommand());
            Commands.Add("fullSearch", new FullSearchCommand());
            Commands.Add("itemSearch", new ItemSearchCommand());
            Commands.Add("pPrev", new PlayerPreviewCommand());
            Commands.Add("iPrev", new ItemPreviewCommand());
            Commands.Add("trackSearch", new TrackSearchCommand());
            Commands.Add("playerName", new PlayerNameCommand());
            //Commands.Add("subscribe", new SubscribeCommand());
            //Commands.Add("unsubscribe", new UnsubscribeCommand());
            Commands.Add("pricerdicer", new PricerDicerCommand());
            Commands.Add("recentAuctions", new GetRecentAuctionsCommand());
            Commands.Add("activeAuctions", new GetActiveAuctionsCommand());
            Commands.Add("paymentSession", new CreatePaymentCommand());
            Commands.Add("premiumExpiration", new PremiumExpirationCommand());
            Commands.Add("setConId", new SetConnectionIdCommand());
            Commands.Add("getRefInfo", new GetRefInfoCommand());
            Commands.Add("setRef", new SetMyReferalCommand());
            Commands.Add("conMc", new ConnectMCAccountCommand());


            Commands.Add("subscribe", new PushSubscribeCommand());
            Commands.Add("unsubscribe", new DeleteSubscriptionCommand());
            Commands.Add("unsubscribeAll", new DeleteAllSubscriptionsCommand());
            Commands.Add("subscriptions", new GetSubscriptionsCommand());
            Commands.Add("token", new RegisterPushTokenCommand());
            Commands.Add("addDevice", new RegisterPushTokenCommand());
            Commands.Add("getDevices", new GetDeviceListCommand());
            Commands.Add("deleteDevice", new DeleteDeviceCommand());
            Commands.Add("testNotification", new SendTestNotificationCommand());


            Commands.Add("setGoogle", new SetGoogleIdCommand());
            Commands.Add("genToken", new GenerateTokenFor());
            Commands.Add("loginExt", new LoginExternalCommand());
            Commands.Add("accountInfo", new AccountInfoCommand());


            Commands.Add("getCoflOwned", new GetCoflOwnerShipCommand());
            Commands.Add("getCoflBalance", new GetCoflBalanceCommand());
            Commands.Add("transferCofl", new TransferCoinsCommand());
            Commands.Add("getProducts", new GetProductsCommand());
            Commands.Add("getPrices", new GetPricesCommand());
            Commands.Add("gPurchase", new GooglePurchaseCommand());
            Commands.Add("paypalPurchase", new ValidatePaypalCommand());

            Commands.Add("getFilter", new GetFilterOptionsCommand());
            Commands.Add("filterFor", new GetFilterForCommand());
            Commands.Add("flipFilters", new GetFlipFiltersCommand());

            Commands.Add("subFlip", new SubFlipperCommand());
            Commands.Add("unsubFlip", new UnsubFlipperCommand());
            Commands.Add("getFlips", new RecentFlipsCommand());
            Commands.Add("flipBased", new BasedOnCommand());
            Commands.Add("authCon", new AuthorizeConnectionCommand());

            // sync commands
            Commands.Add("playerSync", new PlayerSyncCommand());
            Commands.Add("itemSync", new ItemSyncCommand());
            Commands.Add("pricesSync", new PricesSyncCommand());
            Commands.Add("auctionSync", new AuctionSyncCommand());


            Commands.Add("newPlayers", new NewPlayersCommand());
            Commands.Add("newItems", new NewItemsCommand());
            Commands.Add("popularSearches", new PopularSearchesCommand());
            Commands.Add("endedAuctions", new EndedAuctionsCommand());
            Commands.Add("newAuctions", new NewAuctionsCommand());
            Commands.Add("p", new PingCommand());


            FlipperService.Instance.OnSettingsChange += settings =>
            {
                foreach (var item in SkyblockBackEnd.GetConnectionsOfUser(settings.UserId))
                {
                    item.UpdateSettings(settings);
                }
            };
        }

        public SkyblockBackEnd()
        {
            limiter = TimeLimiter.GetFromMaxCountByInterval(5, TimeSpan.FromSeconds(2));
        }

        int waiting = 0;

        protected override void OnMessage(MessageEventArgs e)
        {
            long mId = 0;
            try
            {
                SocketMessageData data;

                if (e.IsText)
                {
                    string body = e.Data;
                    data = ParseData(body);
                }
                else
                    data = MessagePackSerializer.Deserialize<SocketMessageData>(e.RawData);

                mId = data.mId;
                data.Connection = this;

                if (!Commands.ContainsKey(data.Type))
                {
                    data.SendBack(new MessageData("error", $"The command `{data.Type}` is Unkown, please check your spelling"));
                    return;
                }

                if (waiting > 20)
                {
                    dev.Logger.Instance.Error("triggered rate limit");
                    throw new CoflnetException("stop_it", "Your connection is sending to many requests. Please slow down.");
                }

                ExecuteCommand(data);
            }
            catch (CoflnetException ex)
            {

                SendBack(new MessageData("error", JsonConvert.SerializeObject(new { ex.Slug, ex.Message })) { mId = mId });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                SendBack(new MessageData("error", "The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}") { mId = mId });

                throw ex;
            }
        }

        private void ExecuteCommand(SocketMessageData data)
        {
            Task.Run(async () =>
            {
                System.Threading.Interlocked.Increment(ref waiting);
                await limiter;
                System.Threading.Interlocked.Decrement(ref waiting);
                try
                {
                    if (Commands[data.Type].Cacheable && (await CacheService.Instance.TryFromCacheAsync(data)).IsFlagSet(CacheStatus.VALID))
                        return;
                    var tracer = OpenTracing.Util.GlobalTracer.Instance;
                    var builder = tracer.BuildSpan(data.Type)
                            .WithTag("type", "websocket")
                            .WithTag("body", data.Data.Truncate(20));

                    using (var scope = builder.StartActive(true))
                    {
                        var span = scope.Span;
                        data.Span = span;
                        try
                        {
                            await Commands[data.Type].Execute(data);
                        }
                        catch (Exception e)
                        {
                            data.LogError(e, "");
                            throw;
                        }
                    }
                }
                catch (CoflnetException ex)
                {
                    await SendCoflnetException(data, ex);
                }
                catch (Exception ex)
                {
                    var cofl = ex.InnerException as CoflnetException;
                    if (cofl != null)
                    {
                        // wrapped exception (eg. Theaded)
                        await SendCoflnetException(data, cofl);
                        return;
                    }
                    dev.Logger.Instance.Error($"Fatal error on Command {JsonConvert.SerializeObject(data)} {ex.Message} {ex.StackTrace} \n{ex.InnerException?.Message} {ex.InnerException?.StackTrace}");
                    await data.SendBack(new MessageData("error", JsonConvert.SerializeObject(new { Slug = "unknown", Message = "An unexpected error occured, make sure the format of Data is correct" })) { mId = data.mId });
                }
            }).ConfigureAwait(false);
        }

        private static Task SendCoflnetException(SocketMessageData data, CoflnetException ex)
        {
            return data.SendBack(new MessageData("error", JsonConvert.SerializeObject(new { ex.Slug, ex.Message })) { mId = data.mId });
        }

        private static SocketMessageData ParseData(string body)
        {
            var data = MessagePackSerializer.Deserialize<SocketMessageData>(MessagePackSerializer.FromJson(body));
            if (data.Data != null)
                data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
            return data;
        }




        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            Console.WriteLine("=============================\nclosed socket because error");
            Console.WriteLine(e.Message);
            Console.WriteLine(e.Exception.Message);
            Close();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Console.WriteLine(e.Reason);
            Close();
        }

        private new void Close()
        {
            Subscribers.TryRemove(Id, out SkyblockBackEnd value);
            NextUpdateStart -= SendNextUpdate;
        }

        private void SendNextUpdate()
        {
            TrySendData(new MessageData("nextUpdate", ""));
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                this.TrySendData(new MessageData("ping", null));
            });
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            SetConnectionId(this.ID);
            NextUpdateStart += SendNextUpdate;
        }

        public void SetConnectionId(string stringId)
        {
            IncreaseRateLimit();
            long id = GetSessionId(stringId);
            long oldId = this.Id;
            this.Id = id;
            if (id == 0)
                return;

            if (Subscribers.TryRemove(id, out SkyblockBackEnd value))
            {
                // there was an old session, clean up
                // Todo (currently nothing to clean)
            }
            // remove previously set id
            Subscribers.TryRemove(oldId, out value);

            Subscribers.AddOrUpdate(id, this, (key, old) => this);
        }

        /// <summary>
        /// Increases the connection rate limit, after wating for the ip rate limit
        /// </summary>
        private void IncreaseRateLimit()
        {
            Task.Run(async () =>
            {
                await IpRateLimiter.Instance.WaitUntilAllowed(this.Context.Headers["X-Real-Ip"]);

                var constraint2 = new CountByIntervalAwaitableConstraint(10, TimeSpan.FromSeconds(2));
                var heavyUsage = new CountByIntervalAwaitableConstraint(40, TimeSpan.FromSeconds(20));

                limiter = TimeLimiter.Compose(constraint2, heavyUsage);
            }).ConfigureAwait(false);
        }

        private long GetSessionId(string stringId)
        {
            stringId = stringId ?? this.Context.CookieCollection["id"]?.Value;
            stringId = stringId ?? this.Context.QueryString["id"];

            long id = 0;
            if (stringId != null && stringId.Length > 4)
                id = ((long)stringId.Substring(0, stringId.Length / 2).GetHashCode()) << 32 + stringId.Substring(stringId.Length / 2, stringId.Length / 2).GetHashCode();
            return id;
        }
        public static bool SendTo(MessageData data, long connectionId)
        {
            var connected = Subscribers.TryGetValue(connectionId, out SkyblockBackEnd value);
            if (connected)
                value.SendBack(data);

            return connected;
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }

        public async Task<bool> SendFlip(FlipInstance flip)
        {
            await FlipperService.FillVisibilityProbs(flip, this.Settings);
            if (Settings == null || !Settings.MatchesSettings(flip).Item1)
                return true;
            var data = new MessageData("flip", JSON.Stringify(flip), 60);
            FlipSendCount.Inc();
            return TrySendData(data);
        }

        private bool TrySendData(MessageData data)
        {
            if (ConnectionState != WebSocketState.Open)
                return false;
            data.mId = SubFlipMsgId;
            try
            {
                SendBack(data);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "could not send back");
                return false;
            }
            return true;
        }

        public Task<bool> SendSold(string uuid)
        {
            return Task.FromResult(TrySendData(new MessageData("sold", uuid)));
        }

        public void UpdateSettings(SettingsChange settings)
        {
            this.LatestSettings = settings;
            if (!TrySendData(new MessageData("settingsUpdate", JsonConvert.SerializeObject(settings.Settings))))
                FlipperService.Instance.RemoveConnection(this);
        }

        public static IEnumerable<SkyblockBackEnd> GetConnectionsOfUser(long userId)
        {
            foreach (var item in Subscribers)
            {
                var uId = item.Value._userId;
                if (uId != 0 && uId == userId)
                    yield return item.Value;
            }
        }

        public Task<bool> SendFlip(LowPricedAuction flip)
        {
            return SendFlip(FlipperService.LowPriceToFlip(flip));
        }

        void IFlipConnection.Log(string message, Microsoft.Extensions.Logging.LogLevel level)
        {
            // has no log target
        }
    }
}