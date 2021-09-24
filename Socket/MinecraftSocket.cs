using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using hypixel;
using Newtonsoft.Json;
using OpenTracing.Util;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Commands
{
    public class MinecraftSocket : WebSocketBehavior, IFlipConnection
    {
        public string Uuid;

        public long Id => (Uuid + connectionId).GetHashCode();

        protected string connectionId;

        public FlipSettings Settings { get; set; }
        public string Version { get; private set; }
        OpenTracing.ITracer tracer = GlobalTracer.Instance;
        OpenTracing.ISpan conSpan;


        private static FlipSettings DEFAULT_SETTINGS = new FlipSettings() { MinProfit = 100000, MinVolume = 50 };

        protected override void OnOpen()
        {
            conSpan = tracer.BuildSpan("mcConnection").Start();
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            if (args["uuid"] == null)
                Send(Response.Create("error", "the connection query string needs to include uuid"));
            if (args["SId"] != null)
                connectionId = args["SId"].Truncate(60);
            if (args["version"] != null)
                Version = args["version"].Truncate(10);


            Uuid = args["uuid"];
            conSpan.SetTag("uuid", Uuid);
            Console.Write($"Version: {Version} ");
            Console.WriteLine(Uuid);
            var key = new Random().Next();
            base.OnOpen();

            if (Settings == null)
                Settings = DEFAULT_SETTINGS;
            FlipperService.Instance.AddNonConnection(this);
            SendMessage("§6C§1oflnet§8: §fNOTE §7This is a development preview, it is NOT stable/bugfree", $"https://discord.gg/wvKXfTgCfb");
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (true)
                {
                    SendMessage("§6C§1oflnet§8: §lPlease click this [LINK] to login and configure your flip filters §8(you won't receive real time flips until you do)", $"https://sky-commands.coflnet.com/authmod?uuid={Uuid}&conId={Id}");
                    await Task.Delay(TimeSpan.FromSeconds(60));

                    if (Settings != DEFAULT_SETTINGS)
                        return;
                    SendMessage("do /cofl stop to stop receiving this (or click this message)", "/cofl stop");
                }
            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            using var span = tracer.BuildSpan("modCommand").AsChildOf(conSpan.Context).StartActive();
            base.OnMessage(e);
            Console.WriteLine("received message from mcmod " + e.Data);
            var a = JsonConvert.DeserializeObject<Response>(e.Data);
            span.Span.SetTag("type", a.type);
            if (connectionId.StartsWith("debug"))
                SendMessage("executed " + a.data, "");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            FlipperService.Instance.RemoveConnection(this);
            conSpan.Finish();
        }

        public void SendMessage(string text, string clickAction = null, string hoverText = null)
        {
            try
            {
                this.Send(Response.Create("writeToChat", new { text, onClick = clickAction, hover = hoverText }));
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Log("removing connection because " + e.Message);
                using var span = tracer.BuildSpan("modDisconnect").WithTag("error","true").AsChildOf(conSpan.Context).StartActive();
                span.Span.Log(e.Message);
                OnClose(null);
            }
        }

        public void Send(Response response)
        {
            var json = JsonConvert.SerializeObject(response);
            this.Send(json);
        }

        public bool SendFlip(FlipInstance flip)
        {
            if (!(flip.Bin && Settings != null && Settings.MatchesSettings(flip) && !flip.Sold))
                return true;

            using var span = tracer.BuildSpan("modFlip").WithTag("uuid", flip.Uuid).AsChildOf(conSpan.Context).StartActive();
            SendMessage(GetFlipMsg(flip), "/viewauction " + flip.Uuid, string.Join('\n', flip.Interesting.Select(s => "・" + s)));
            return true;
        }

        private string GetFlipMsg(FlipInstance flip)
        {
            return $"FLIP: {GetRarityColor(flip.Rarity)}{flip.Name} §f{FormatPrice(flip.LastKnownCost)} -> {FormatPrice(flip.MedianPrice)} §g[BUY]";
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
        private static string FormatPrice(long price)
        {
            return string.Format("{0:n0}", price);
        }

        public bool SendSold(string uuid)
        {
            // don't send extra messages
            return true;
        }

        public void UpdateSettings(SettingsChange settings)
        {
            if (this.Settings == DEFAULT_SETTINGS)
            {
                using var span = tracer.BuildSpan("modAuthorized").AsChildOf(conSpan.Context).StartActive();
                SendMessage($"Authorized connection you can now control settings via the website");
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    SendMessage($"Remember: the format of the flips is: §dITEM NAME §fCOST -> MEDIAN");
                });
            }
            else
                SendMessage($"setting changed " + FindWhatsNew(this.Settings,settings.Settings));
            Settings = settings.Settings;

            if (settings.Tier.HasFlag(AccountTier.PREMIUM))
                FlipperService.Instance.AddConnection(this);
            else
                FlipperService.Instance.AddNonConnection(this);
        }

        private string FindWhatsNew(FlipSettings current, FlipSettings newSettings)
        {
            if(current.MinProfit != newSettings.MinProfit)
                return "min Profit to " + FormatPrice(newSettings.MinProfit);
            if(current.BlackList.Count < newSettings.BlackList.Count)
                return $"blacklisted item";
            if(current.WhiteList.Count < newSettings.WhiteList.Count)
                return $"whitelisted item";

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
}