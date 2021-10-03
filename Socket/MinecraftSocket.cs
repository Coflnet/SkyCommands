using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Coflnet.Sky.Filter;
using hypixel;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Newtonsoft.Json;
using OpenTracing.Util;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Commands.MC
{
    public class MinecraftSocket : WebSocketBehavior, IFlipConnection
    {
        public string Uuid;

        public long Id { get; private set; }

        protected string sessionId = "";

        public FlipSettings Settings { get; set; }
        public string Version { get; private set; }
        OpenTracing.ITracer tracer = new Jaeger.Tracer.Builder("sky-commands-mod").WithSampler(new ConstSampler(true)).Build();
        OpenTracing.ISpan conSpan;
        private System.Threading.Timer PingTimer;

        private static FlipSettings DEFAULT_SETTINGS = new FlipSettings() { MinProfit = 100000, MinVolume = 50 };

        public static ClassNameDictonary<McCommand> Commands = new ClassNameDictonary<McCommand>();

        static MinecraftSocket()
        {
            Commands.Add<TestCommand>();
        }


        protected override void OnOpen()
        {
            conSpan = tracer.BuildSpan("connection").Start();
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            if (args["uuid"] == null)
                Send(Response.Create("error", "the connection query string needs to include uuid"));
            if (args["SId"] != null)
                sessionId = args["SId"].Truncate(60);
            if (args["version"] != null)
                Version = args["version"].Truncate(10);

            Uuid = args["uuid"];
            conSpan.SetTag("uuid", Uuid);
            Console.Write($"Version: {Version} ");
            Console.WriteLine(Uuid);

            string stringId;
            (this.Id, stringId) = ComputeConnectionId();
            conSpan.SetTag("conId", stringId);

            base.OnOpen();

            if (Settings == null)
                Settings = DEFAULT_SETTINGS;
            FlipperService.Instance.AddNonConnection(this);
            SendMessage("§1C§6oflnet§8: §fNOTE §7This is a development preview, it is NOT stable/bugfree", $"https://discord.gg/wvKXfTgCfb");
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
                    this.Settings = cachedSettings.Settings;
                    UpdateConnectionTier(cachedSettings);
                    await SendAuthorizedHello(cachedSettings);
                    SendMessage($"§1C§6oflnet§8: Found and loaded settings for your connection, e.g. MinProfit: {FormatPrice(Settings.MinProfit)}\n "
                        + "§f: click this if you want to change a setting \n"
                        + "§8: nothing else to do have a nice day :)",
                        "https://sky-commands.coflnet.com/flipper");
                    Console.WriteLine($"loaded settings for {this.sessionId} " + JsonConvert.SerializeObject(cachedSettings));
                    return;
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "loading modsocket");
                }
            }
            while (true)
            {
                SendMessage("§1C§6oflnet§8: §lPlease click this [LINK] to login and configure your flip filters §8(you won't receive real time flips until you do)",
                    $"https://sky-commands.coflnet.com/authmod?uuid={Uuid}&conId={HttpUtility.UrlEncode(stringId)}");
                await Task.Delay(TimeSpan.FromSeconds(60));

                if (Settings != DEFAULT_SETTINGS)
                    return;
                SendMessage("do /cofl stop to stop receiving this (or click this message)", "/cofl stop");
            }
        }

        private async Task SendAuthorizedHello(SettingsChange cachedSettings)
        {
            var mcNameTask = PlayerService.Instance.GetPlayer(this.Uuid);
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
            SendMessage($"§1C§6oflnet§8: Hello {(await mcNameTask)?.Name} ({anonymisedEmail})");
            if (cachedSettings.Tier != AccountTier.NONE && cachedSettings.ExpiresAt > DateTime.Now)
                SendMessage($"§1C§6oflnet§8: You have {cachedSettings.Tier.ToString()} until {cachedSettings.ExpiresAt}");
            else
                SendMessage($"§1C§6oflnet§8: You use the free version of the flip finder");
        }

        private void SendPing()
        {
            using var span = tracer.BuildSpan("ping").AsChildOf(conSpan.Context).StartActive();
            try
            {
                Send(Response.Create("ping", 0));
            }
            catch (Exception e)
            {
                span.Span.Log("could not send ping");

                CloseBecauseError(e);
            }
        }

        protected (long, string) ComputeConnectionId()
        {
            var bytes = Encoding.UTF8.GetBytes(Uuid.ToLower() + sessionId + DateTime.Now.Date.ToString());
            var hash = System.Security.Cryptography.SHA512.Create();
            var hashed = hash.ComputeHash(bytes);
            return (BitConverter.ToInt64(hashed), Convert.ToBase64String(hashed, 0, 16).Replace('+', '-').Replace('/', '_'));
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            using var span = tracer.BuildSpan("Command").AsChildOf(conSpan.Context).StartActive();
            base.OnMessage(e);
            Console.WriteLine("received message from mcmod " + e.Data);
            var a = JsonConvert.DeserializeObject<Response>(e.Data);
            if (a == null || a.type == null)
            {
                Send(new Response("error", "the payload has to have the property type"));
                return;
            }
            span.Span.SetTag("type", a.type);
            if (sessionId.StartsWith("debug"))
                SendMessage("executed " + a.data, "");

            // block click commands for now
            if (a.type == "tokenLogin" || a.type == "clicked")
                return;

            if (Commands.TryGetValue(a.type.ToLower(), out McCommand command))
                command.Execute(this, a.data);
            else
                SendMessage($"The command {a.type} is not know. Please check your spelling ;)");

        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            FlipperService.Instance.RemoveConnection(this);

            PingTimer.Dispose();
            conSpan.Finish();
        }

        public void SendMessage(string text, string clickAction = null, string hoverText = null)
        {
            if (ConnectionState != WebSocketState.Open)
            {
                using var span = tracer.BuildSpan("removing").AsChildOf(conSpan).StartActive();
                FlipperService.Instance.RemoveConnection(this);
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

        public void SendSound(string soundId)
        {
            this.Send(Response.Create("playSound", soundId));
        }

        private OpenTracing.IScope CloseBecauseError(Exception e)
        {
            dev.Logger.Instance.Log("removing connection because " + e.Message);
            dev.Logger.Instance.Error(System.Environment.StackTrace);
            var span = tracer.BuildSpan("Disconnect").WithTag("error", "true").AsChildOf(conSpan.Context).StartActive();
            span.Span.Log(e.Message);
            OnClose(null);
            return span;
        }

        public void Send(Response response)
        {
            var json = JsonConvert.SerializeObject(response);
            this.Send(json);
        }

        public bool SendFlip(FlipInstance flip)
        {
            if (base.ConnectionState != WebSocketState.Open)
                return false;
            if (!(flip.Bin && Settings != null && Settings.MatchesSettings(flip) && !flip.Sold))
                return true;

            using var span = tracer.BuildSpan("Flip").WithTag("uuid", flip.Uuid).AsChildOf(conSpan.Context).StartActive();
            SendMessage(GetFlipMsg(flip), "/viewauction " + flip.Uuid, string.Join('\n', flip.Interesting.Select(s => "・" + s)));
            PingTimer.Change(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(55));
            return true;
        }

        private string GetFlipMsg(FlipInstance flip)
        {
            var priceColor = GetProfitColor(flip.MedianPrice - flip.LastKnownCost);
            return $"\nFLIP: {GetRarityColor(flip.Rarity)}{flip.Name} {priceColor}{FormatPrice(flip.LastKnownCost)} -> {FormatPrice(flip.MedianPrice)} §g[BUY]";
        }

        private string GetRarityColor(Tier rarity)
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

        private string GetProfitColor(int profit)
        {
            if (profit >= 50_000_000)
                return McColorCodes.GOLD;
            if (profit >= 10_000_000)
                return McColorCodes.BLUE;
            if (profit >= 1_000_000)
                return McColorCodes.GREEN;
            if (profit >= 100_000)
                return McColorCodes.DARK_GREEN;
            return McColorCodes.DARK_GRAY;
        }

        private static string FormatPrice(long price)
        {
            return string.Format("{0:n0}", price);
        }

        public bool SendSold(string uuid)
        {
            if (base.ConnectionState != WebSocketState.Open)
                return false;
            // don't send extra messages
            return true;
        }

        public void UpdateSettings(SettingsChange settings)
        {
            using var span = tracer.BuildSpan("SettingsUpdate").AsChildOf(conSpan.Context).StartActive();
            if (this.Settings == DEFAULT_SETTINGS)
            {
                Task.Run(async () =>
                {
                    using var span = tracer.BuildSpan("Authorized").AsChildOf(conSpan.Context).StartActive();
                    try
                    {
                        await SendAuthorizedHello(settings);
                        SendMessage($"Authorized connection you can now control settings via the website");
                        await Task.Delay(TimeSpan.FromSeconds(20));
                        SendMessage($"Remember: the format of the flips is: §dITEM NAME §fCOST -> MEDIAN");
                    }
                    catch (Exception e)
                    {
                        dev.Logger.Instance.Error(e, "settings authorization");
                        span.Span.Log(e.Message);
                    }
                });
            }
            else
                SendMessage($"setting changed " + FindWhatsNew(this.Settings, settings.Settings));
            Settings = settings.Settings;
            UpdateConnectionTier(settings);

            CacheService.Instance.SaveInRedis(this.Id.ToString(), settings, TimeSpan.FromHours(6));
        }

        private void UpdateConnectionTier(SettingsChange settings)
        {
            if (settings.Tier.HasFlag(AccountTier.PREMIUM) && settings.ExpiresAt > DateTime.Now)
                FlipperService.Instance.AddConnection(this);
            else
                FlipperService.Instance.AddNonConnection(this);
        }

        private string FindWhatsNew(FlipSettings current, FlipSettings newSettings)
        {
            try
            {
                if (current.MinProfit != newSettings.MinProfit)
                    return "min Profit to " + FormatPrice(newSettings.MinProfit);
                if (current.MinProfit != newSettings.MinProfit)
                    return "max Cost to " + FormatPrice(newSettings.MaxCost);
                if (current.BlackList?.Count < newSettings.BlackList.Count)
                    return $"blacklisted item";
                if (current.WhiteList?.Count < newSettings.WhiteList.Count)
                    return $"whitelisted item";
            }
            catch (Exception e)
            {
                this.conSpan.Log(e.StackTrace);
            }

            return "";
        }

        public class Response
        {
            public string type;
            public string data;

            public Response()
            {
            }

            public Response(string type, string data)
            {
                this.type = type;
                this.data = data;
            }

            public static Response Create<T>(string type, T data)
            {
                return new Response(type, JsonConvert.SerializeObject(data));
            }

        }

    }

    public class McColorCodes
    {
        public static readonly string BLACK = "§0";
        public static readonly string DARK_BLUE = "§1";
        public static readonly string DARK_GREEN = "§2";
        public static readonly string DARK_AQUA = "§3";
        public static readonly string DARK_RED = "§4";
        public static readonly string DARK_PURPLE = "§5";
        public static readonly string GOLD = "§6";
        public static readonly string DARK = "§7";
        public static readonly string DARK_GRAY = "§8";
        public static readonly string BLUE = "§9";
        public static readonly string GREEN = "§a";
        public static readonly string AQUA = "§b";
        public static readonly string RED = "§c";
        public static readonly string LIGHT_PURPLE = "§d";
        public static readonly string YELLOW = "§e";
        public static readonly string WHITE = "§f";
        public static readonly string MINECOIN_GOLD = "§g";
    }
}