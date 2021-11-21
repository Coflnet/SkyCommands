using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Coflnet.Sky.Commands.Helper;
using Coflnet.Sky.Filter;
using hypixel;
using Jaeger.Samplers;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Commands.MC
{
    public partial class MinecraftSocket : WebSocketBehavior, IFlipConnection
    {
        public string McId;
        public static string COFLNET = "[§1C§6oflnet§f]§7: ";

        public long Id { get; private set; }

        protected string sessionId = "";

        public FlipSettings Settings => LastSettingsChange.Settings;
        public int UserId => LastSettingsChange.UserId;
        private SettingsChange LastSettingsChange { get; set; } = new SettingsChange();

        public string Version { get; private set; }
        public OpenTracing.ITracer tracer = new Jaeger.Tracer.Builder("sky-commands-mod").WithSampler(new ConstSampler(true)).Build();
        public OpenTracing.ISpan ConSpan { get; private set; }
        private System.Threading.Timer PingTimer;

        public IModVersionAdapter ModAdapter;

        public static FlipSettings DEFAULT_SETTINGS = new FlipSettings() { MinProfit = 100000, MinVolume = 50, ModSettings = new ModSettings(), Visibility = new VisibilitySettings() };

        public static ClassNameDictonary<McCommand> Commands = new ClassNameDictonary<McCommand>();

        public static event Action NextUpdateStart;
        private int blockedFlipCount;
        private int blockedFlipFilterCount;

        private static System.Threading.Timer updateTimer;

        private ConcurrentDictionary<long, DateTime> SentFlips = new ConcurrentDictionary<long, DateTime>();
        public ConcurrentQueue<BlockedElement> TopBlocked = new ConcurrentQueue<BlockedElement>();

        public class BlockedElement
        {
            public FlipInstance Flip;
            public string Reason;
        }
        private static Prometheus.Counter sentFlipsCount = Prometheus.Metrics.CreateCounter("sky_commands_sent_flips", "How many flip messages were sent");

        static MinecraftSocket()
        {
            Commands.Add<TestCommand>();
            Commands.Add<SoundCommand>();
            Commands.Add<ReferenceCommand>();
            Commands.Add<ReportCommand>();
            Commands.Add<PurchaseStartCommand>();
            Commands.Add<PurchaseConfirmCommand>();
            Commands.Add<ClickedCommand>();
            Commands.Add<ResetCommand>();
            Commands.Add<OnlineCommand>();
            Commands.Add<BlacklistCommand>();
            Commands.Add<SniperCommand>();
            Commands.Add<ExactCommand>();
            Commands.Add<BlockedCommand>();
            Commands.Add<ExperimentalCommand>();
            Commands.Add<NormalCommand>();

            Task.Run(async () =>
            {
                var next = await new NextUpdateRetriever().Get();

                NextUpdateStart += () =>
                {
                    Console.WriteLine("next update");
                };
                while (next < DateTime.Now)
                    next += TimeSpan.FromMinutes(1);
                Console.WriteLine($"started timer to start at {next} now its {DateTime.Now}");
                updateTimer = new System.Threading.Timer((e) =>
                {
                    try
                    {
                        NextUpdateStart?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        dev.Logger.Instance.Error(ex, "sending next update");
                    }
                }, null, next - DateTime.Now, TimeSpan.FromMinutes(1));
            }).ConfigureAwait(false);
        }

        protected override void OnOpen()
        {
            ConSpan = tracer.BuildSpan("connection").Start();
            base.OnOpen();
            Task.Run(() =>
            {
                using var openSpan = tracer.BuildSpan("open").AsChildOf(ConSpan).StartActive();
                try
                {
                    StartConnection(openSpan);
                }
                catch (Exception e)
                {
                    Error(e, "starting connection");
                }
            }).ConfigureAwait(false);

        }

        private void StartConnection(OpenTracing.IScope openSpan)
        {
            SendMessage(COFLNET + "§fNOTE §7This is a development preview, it is NOT stable/bugfree", $"https://discord.gg/wvKXfTgCfb", System.Net.Dns.GetHostName());
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            if (args["uuid"] == null && args["player"] == null)
                Send(Response.Create("error", "the connection query string needs to include 'player'"));
            if (args["SId"] != null)
                sessionId = args["SId"].Truncate(60);
            if (args["version"] != null)
                Version = args["version"].Truncate(10);

            ModAdapter = Version switch
            {
                "1.3-Alpha" => new ThirdVersionAdapter(this),
                "1.2-Alpha" => new SecondVersionAdapter(this),
                _ => new FirstModVersionAdapter(this)
            };

            McId = args["player"] ?? args["uuid"];
            ConSpan.SetTag("uuid", McId);
            ConSpan.SetTag("version", Version);

            string stringId;
            (this.Id, stringId) = ComputeConnectionId();
            ConSpan.SetTag("conId", stringId);


            if (Settings == null)
                LastSettingsChange.Settings = DEFAULT_SETTINGS;
            FlipperService.Instance.AddNonConnection(this, false);
            System.Threading.Tasks.Task.Run(async () =>
            {
                await SetupConnectionSettings(stringId);
            }).ConfigureAwait(false);

            PingTimer = new System.Threading.Timer((e) =>
            {
                SendPing();
            }, null, TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(50));
        }

        private async Task SetupConnectionSettings(string stringId)
        {
            var cachedSettings = await CacheService.Instance.GetFromRedis<SettingsChange>(this.Id.ToString());
            if (cachedSettings != null)
            {
                try
                {
                    MigrateSettings(cachedSettings);
                    this.LastSettingsChange = cachedSettings;
                    UpdateConnectionTier(cachedSettings);
                    await SendAuthorizedHello(cachedSettings);
                    // set them again
                    this.LastSettingsChange = cachedSettings;
                    SendMessage(COFLNET + $"§fFound and loaded settings for your connection\n"
                        + $" MinProfit: {FormatPrice(Settings.MinProfit)}  "
                        + $" MaxCost: {FormatPrice(Settings.MaxCost)}"
                        + $" Blacklist-Size: {Settings?.BlackList?.Count ?? 0}\n "
                        + (Settings.BasedOnLBin ? $" Your profit is based on Lowest bin, please not that this is NOT the intended way to use this\n " : "")
                        + "§f: click this if you want to change a setting \n"
                        + "§8: nothing else to do have a nice day :)",
                        "https://sky.coflnet.com/flipper");
                    Console.WriteLine($"loaded settings for {this.sessionId} " + JsonConvert.SerializeObject(cachedSettings));
                    await Task.Delay(500);
                    SendMessage(COFLNET + $"{McColorCodes.DARK_GREEN} click this to relink your account",
                    GetAuthLink(stringId), "You don't need to relink your account. \nThis is only here to allow you to link your mod to the website again should you notice your settings aren't updated");
                    return;
                }
                catch (Exception e)
                {
                    Error(e, "loading modsocket");
                    SendMessage(COFLNET + $"Your settings could not be loaded, please relink again :)");
                }
            }
            while (true)
            {
                SendMessage(COFLNET + "§lPlease click this [LINK] to login and configure your flip filters §8(you won't receive real time flips until you do)",
                    GetAuthLink(stringId));
                await Task.Delay(TimeSpan.FromSeconds(60));

                if (Settings != DEFAULT_SETTINGS)
                    return;
                SendMessage("do /cofl stop to stop receiving this (or click this message)", "/cofl stop");
            }
        }

        private static void MigrateSettings(SettingsChange cachedSettings)
        {
            if (cachedSettings.Settings.AllowedFinders == LowPricedAuction.FinderType.UNKOWN || cachedSettings.Version < 1)
                cachedSettings.Settings.AllowedFinders = LowPricedAuction.FinderType.FLIPPER;
            cachedSettings.Version = 1;
        }

        private string GetAuthLink(string stringId)
        {
            return $"https://sky.coflnet.com/authmod?mcid={McId}&conId={HttpUtility.UrlEncode(stringId)}";
        }

        private async Task SendAuthorizedHello(SettingsChange cachedSettings)
        {
            var mcName = this.McId.Length == 32 ? (await PlayerService.Instance.GetPlayer(this.McId)).Name : this.McId;
            var user = UserService.Instance.GetUserById(cachedSettings.UserId);
            var length = user.Email.Length < 10 ? 3 : 6;
            var builder = new StringBuilder(user.Email);
            for (int i = 0; i < builder.Length - 5; i++)
            {
                if (builder[i] == '@' || i < 3)
                    continue;
                builder[i] = '*';
            }
            var anonymisedEmail = builder.ToString();
            var messageStart = $"Hello {mcName} ({anonymisedEmail}) \n";
            if (cachedSettings.Tier != AccountTier.NONE && cachedSettings.ExpiresAt > DateTime.Now)
                SendMessage(COFLNET + messageStart + $"You have {cachedSettings.Tier.ToString()} until {cachedSettings.ExpiresAt}");
            else
                SendMessage(COFLNET + messageStart + $"You use the free version of the flip finder");

            await Task.Delay(300);
        }

        private void SendPing()
        {
            using var span = tracer.BuildSpan("ping").AsChildOf(ConSpan.Context).WithTag("count", blockedFlipFilterCount).StartActive();
            try
            {
                if (blockedFlipFilterCount > 0)
                {
                    SendMessage(COFLNET + $"there were {blockedFlipFilterCount} flips blocked by your filter the last minute");//, "/cofl blocked", "click to list the best 5 of the last min");
                    blockedFlipFilterCount = 0;
                }
                else
                {
                    Send(Response.Create("ping", 0));

                    UpdateConnectionTier(LastSettingsChange);
                }
            }
            catch (Exception e)
            {
                span.Span.Log("could not send ping");
                CloseBecauseError(e);
            }
        }

        protected (long, string) ComputeConnectionId()
        {
            var bytes = Encoding.UTF8.GetBytes(McId.ToLower() + sessionId + DateTime.Now.Date.ToString());
            var hash = System.Security.Cryptography.SHA512.Create();
            var hashed = hash.ComputeHash(bytes);
            return (BitConverter.ToInt64(hashed), Convert.ToBase64String(hashed, 0, 16).Replace('+', '-').Replace('/', '_'));
        }

        int waiting = 0;

        protected override void OnMessage(MessageEventArgs e)
        {
            if (waiting > 3)
            {
                SendMessage(COFLNET + $"You are executing to many commands please wait a bit");
                return;
            }
            using var span = tracer.BuildSpan("Command").AsChildOf(ConSpan.Context).StartActive();

            var a = JsonConvert.DeserializeObject<Response>(e.Data);
            if (a == null || a.type == null)
            {
                Send(new Response("error", "the payload has to have the property 'type'"));
                return;
            }
            span.Span.SetTag("type", a.type);
            span.Span.SetTag("content", a.data);
            if (sessionId.StartsWith("debug"))
                SendMessage("executed " + a.data, "");

            // block click commands for now
            if (a.type == "tokenLogin" || a.type == "clicked")
                return;

            if (!Commands.TryGetValue(a.type.ToLower(), out McCommand command))
                SendMessage($"The command '{a.type}' is not know. Please check your spelling ;)");

            Task.Run(async () =>
            {
                waiting++;
                try
                {
                    await command.Execute(this, a.data);
                }
                catch (Exception ex)
                {
                    Error(ex, "mod command");
                }
                finally
                {
                    waiting--;
                }
            });
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            FlipperService.Instance.RemoveConnection(this);
            ConSpan.Log(e?.Reason);

            ConSpan.Finish();
        }

        public void SendMessage(string text, string clickAction = null, string hoverText = null)
        {
            if (ConnectionState != WebSocketState.Open)
            {
                using var span = tracer.BuildSpan("removing").AsChildOf(ConSpan).StartActive();
                FlipperService.Instance.RemoveConnection(this);
                PingTimer.Dispose();
                return;
            }
            try
            {
                this.Send(Response.Create("writeToChat", new { text, onClick = clickAction, hover = hoverText }));
            }
            catch (Exception e)
            {
                CloseBecauseError(e);
            }
        }

        public void SendMessage(params ChatPart[] parts)
        {
            if (ConnectionState != WebSocketState.Open)
            {
                using var span = tracer.BuildSpan("removing").AsChildOf(ConSpan).StartActive();
                FlipperService.Instance.RemoveConnection(this);
                return;
            }
            try
            {
                this.ModAdapter.SendMessage(parts);
            }
            catch (Exception e)
            {
                CloseBecauseError(e);
            }
        }

        public void SendSound(string soundId, float pitch = 1f)
        {
            ModAdapter.SendSound(soundId, pitch);
        }

        private OpenTracing.IScope CloseBecauseError(Exception e)
        {
            dev.Logger.Instance.Log("removing connection because " + e.Message);
            dev.Logger.Instance.Error(System.Environment.StackTrace);
            var span = tracer.BuildSpan("Disconnect").WithTag("error", "true").AsChildOf(ConSpan.Context).StartActive();
            span.Span.Log(e.Message);
            OnClose(null);
            PingTimer.Dispose();
            return span;
        }

        private void Error(Exception exception, string message = null)
        {
            using var error = tracer.BuildSpan("error").WithTag("message", message).WithTag("error", "true").StartActive();
            AddExceptionLog(error, exception);
        }

        private void AddExceptionLog(OpenTracing.IScope error, Exception e)
        {
            error.Span.Log(e.Message);
            error.Span.Log(e.StackTrace);
            if (e.InnerException != null)
                AddExceptionLog(error, e.InnerException);
        }

        public void Send(Response response)
        {
            var json = JsonConvert.SerializeObject(response);
            this.Send(json);
        }

        public async Task<bool> SendFlip(FlipInstance flip)
        {
            try
            {
                if (base.ConnectionState != WebSocketState.Open)
                    return false;
                // pre check already sent flips
                if (SentFlips.ContainsKey(flip.UId))
                    return true; // don't double send
                if (Settings.AllowedFinders != LowPricedAuction.FinderType.UNKOWN && flip.Finder != LowPricedAuction.FinderType.UNKOWN
                        && !Settings.AllowedFinders.HasFlag(flip.Finder)
                        && (int)flip.Finder != 3)
                {
                    BlockedFlip(flip, "finder " + flip.Finder.ToString());
                    return true;
                }
                if (!flip.Bin) // no nonbin 
                    return true;

                if (flip.Sold)
                {
                    BlockedFlip(flip, "sold");
                    return true;
                }
                var isMatch = Settings.MatchesSettings(flip);
                if (Settings != null && !isMatch.Item1)
                {
                    BlockedFlip(flip, isMatch.Item2);
                    blockedFlipFilterCount++;
                    return true;
                }

                // this check is down here to avoid filling up the list
                if (!SentFlips.TryAdd(flip.UId, DateTime.Now))
                    return true; // make sure flips are not sent twice
                using var span = tracer.BuildSpan("Flip").WithTag("uuid", flip.Uuid).AsChildOf(ConSpan.Context).StartActive();
                var settings = Settings;
                await FlipperService.FillVisibilityProbs(flip, settings);

                ModAdapter.SendFlip(flip);
                sentFlipsCount.Inc();

                PingTimer.Change(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(55));
                // remove dupplicates
                if (SentFlips.Count > 300)
                {
                    foreach (var item in SentFlips.Where(i => i.Value < DateTime.Now - TimeSpan.FromMinutes(2)).ToList())
                    {
                        SentFlips.TryRemove(item.Key, out DateTime value);
                    }
                }
            }
            catch (Exception e)
            {
                Error(e, "sending flip");
                return false;
            }
            return true;
        }

        private void BlockedFlip(FlipInstance flip, string reason)
        {
            if (TopBlocked.Count < 3 || TopBlocked.Min(elem => elem.Flip.Profit) < flip.Profit)
            {
                if (TopBlocked.Where(b => b.Flip.Uuid == flip.Uuid).Any())
                    return;
                TopBlocked.Enqueue(new BlockedElement()
                {
                    Flip = flip,
                    Reason = reason
                });
            }
            if (TopBlocked.Count > 5)
            {
                TopBlocked.TryDequeue(out BlockedElement toRemove);
            }
        }

        public string GetFlipMsg(FlipInstance flip)
        {
            var targetPrice = Settings.BasedOnLBin ? (flip.LowestBin ?? 0) : flip.MedianPrice;
            var profit = targetPrice - flip.LastKnownCost;
            var priceColor = GetProfitColor((int)profit);
            var extraText = "\n" + String.Join(", ", flip.Interesting.Take(Settings.Visibility?.ExtraInfoMax ?? 0));

            return $"\nFLIP: {GetRarityColor(flip.Rarity)}{flip.Name} {priceColor}{FormatPrice(flip.LastKnownCost)} -> {FormatPrice(targetPrice)} "
                + $"(+{FormatPrice(profit)} {McColorCodes.DARK_RED}{FormatPrice(flip.ProfitPercentage)}%{priceColor}) §g[BUY]"
                + extraText;
        }

        public string GetRarityColor(Tier rarity)
        {
            return rarity switch
            {
                Tier.COMMON => "§f",
                Tier.EPIC => "§5",
                Tier.UNCOMMON => "§a",
                Tier.RARE => "§9",
                Tier.SPECIAL => "§c",
                Tier.SUPREME => "§4",
                Tier.VERY_SPECIAL => "§4",
                Tier.LEGENDARY => "§6",
                Tier.MYTHIC => "§d",
                _ => ""
            };
        }

        public string GetProfitColor(int profit)
        {
            if (profit >= 50_000_000)
                return McColorCodes.GOLD;
            if (profit >= 10_000_000)
                return McColorCodes.AQUA;
            if (profit >= 1_000_000)
                return McColorCodes.GREEN;
            if (profit >= 100_000)
                return McColorCodes.DARK_GREEN;
            return McColorCodes.DARK_GRAY;
        }

        public string FormatPrice(long price)
        {
            if (Settings.ModSettings?.ShortNumbers ?? false)
                return FormatPriceShort(price);
            return string.Format("{0:n0}", price);
        }

        /// <summary>
        /// By RenniePet on Stackoverflow
        /// https://stackoverflow.com/a/30181106
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private static string FormatPriceShort(long num)
        {
            // Ensure number has max 3 significant digits (no rounding up can happen)
            long i = (long)Math.Pow(10, (int)Math.Max(0, Math.Log10(num) - 2));
            num = num / i * i;

            if (num >= 1000000000)
                return (num / 1000000000D).ToString("0.##") + "B";
            if (num >= 1000000)
                return (num / 1000000D).ToString("0.##") + "M";
            if (num >= 1000)
                return (num / 1000D).ToString("0.##") + "k";

            return num.ToString("#,0");
        }

        public Task<bool> SendSold(string uuid)
        {
            if (base.ConnectionState != WebSocketState.Open)
                return Task.FromResult(false);
            // don't send extra messages
            return Task.FromResult(true);
        }

        public void UpdateSettings(SettingsChange settings)
        {
            var settingsSame = AreSettingsTheSame(settings);
            using var span = tracer.BuildSpan("SettingsUpdate").AsChildOf(ConSpan.Context)
                    .WithTag("premium", settings.Tier.ToString())
                    .WithTag("userId", settings.UserId.ToString())
                    .StartActive();
            if (this.Settings == DEFAULT_SETTINGS)
            {
                Task.Run(async () => await ModGotAuthorised(settings));
            }
            else if (!settingsSame)
                SendMessage($"setting changed " + FindWhatsNew(this.Settings, settings.Settings));
            LastSettingsChange = settings;
            UpdateConnectionTier(settings);

            CacheService.Instance.SaveInRedis(this.Id.ToString(), settings);
            span.Span.Log(JSON.Stringify(settings));
        }

        public Task UpdateSettings(Func<SettingsChange, SettingsChange> updatingFunc)
        {
            var newSettings = updatingFunc(this.LastSettingsChange);
            return FlipperService.Instance.UpdateSettings(newSettings);
        }

        private async Task<OpenTracing.IScope> ModGotAuthorised(SettingsChange settings)
        {
            var span = tracer.BuildSpan("Authorized").AsChildOf(ConSpan.Context).StartActive();
            try
            {
                await SendAuthorizedHello(settings);
                SendMessage($"Authorized connection you can now control settings via the website");
                await Task.Delay(TimeSpan.FromSeconds(20));
                SendMessage($"Remember: the format of the flips is: §dITEM NAME §fCOST -> MEDIAN");
            }
            catch (Exception e)
            {
                Error(e, "settings authorization");
                span.Span.Log(e.Message);
            }

            return span;
        }

        /// <summary>
        /// Tests if the given settings are different from the current active ones
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        private bool AreSettingsTheSame(SettingsChange settings)
        {
            return MessagePack.MessagePackSerializer.Serialize(settings.Settings).SequenceEqual(MessagePack.MessagePackSerializer.Serialize(Settings));
        }

        private void UpdateConnectionTier(SettingsChange settings)
        {
            if ((settings.Tier.HasFlag(AccountTier.PREMIUM) || settings.Tier.HasFlag(AccountTier.STARTER_PREMIUM)) && settings.ExpiresAt > DateTime.Now)
            {
                FlipperService.Instance.AddConnection(this, false);
                NextUpdateStart -= SendTimer;
                NextUpdateStart += SendTimer;
            }
            else
                FlipperService.Instance.AddNonConnection(this, false);
            this.ConSpan.SetTag("tier", settings.Tier.ToString());
        }

        private void SendTimer()
        {
            if (base.ConnectionState != WebSocketState.Open)
            {
                NextUpdateStart -= SendTimer;
                return;
            }
            SendMessage(
                COFLNET + "Flips in 10 seconds",
                null,
                "The Hypixel API will update in 10 seconds. Get ready to receive the latest flips. "
                + "(this is an automated message being sent 50 seconds after the last update)");
            TopBlocked = new ConcurrentQueue<BlockedElement>();
        }

        private string FindWhatsNew(FlipSettings current, FlipSettings newSettings)
        {
            try
            {
                if (current.MinProfit != newSettings.MinProfit)
                    return "min Profit to " + FormatPrice(newSettings.MinProfit);
                if (current.MinProfit != newSettings.MinProfit)
                    return "max Cost to " + FormatPrice(newSettings.MaxCost);
                if (current.BlackList?.Count < newSettings.BlackList?.Count)
                    return $"blacklisted item";
                if (current.WhiteList?.Count < newSettings.WhiteList.Count)
                    return $"whitelisted item";
            }
            catch (Exception e)
            {
                this.ConSpan.Log(e.StackTrace);
            }

            return "";
        }

        public Task<bool> SendFlip(LowPricedAuction flip)
        {
            return SendFlip(FlipperService.LowPriceToFlip(flip));
        }


    }
}
