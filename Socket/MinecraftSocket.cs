using System;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using hypixel;
using Newtonsoft.Json;
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

        private static FlipSettings DEFAULT_SETTINGS = new FlipSettings() { MinProfit = 10000, MinVolume = 50 };

        protected override void OnOpen()
        {
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            if (args["uuid"] == null)
                Send(Response.Create("error", "the connection query string needs to include uuid"));
            if (args["conId"] != null)
                connectionId = args["conId"];

            Uuid = args["uuid"];
            Console.WriteLine(Uuid);
            var key = new Random().Next();
            base.OnOpen();
            
            if (Settings == null)
                Settings = DEFAULT_SETTINGS;
            FlipperService.Instance.AddNonConnection(this);
            SendMessage("§6C§1oflnet§8: §fNOTE $7This is a development preview", $"https://discord.gg/wvKXfTgCfb");
            System.Threading.Tasks.Task.Run(async () =>
            {
                while (true)
                {
                    SendMessage("§6C§1oflnet§8: §lPlease click this [LINK] to login and configure your flip filters §8(you won't receive real time flips until you do)", $"https://sky-commands.coflnet.com/authmod?uuid={Uuid}&conId={Id}");
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    
                    if(Settings != DEFAULT_SETTINGS)
                        return;
                    SendMessage("do /cofl stop to stop receiving this (or click this)","/cofl stop");
                }

            });
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
            Console.WriteLine("received message from mcmod " + e.Data);
            var a = JsonConvert.DeserializeObject<Response>(e.Data);
            SendMessage("received " + a.type + " with " + a.data, "");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            FlipperService.Instance.RemoveConnection(this);
        }

        public void SendMessage(string text, string clickAction, string hoverText = null)
        {
            this.Send(Response.Create("writeToChat", new { text, onClick = clickAction, hover = hoverText }));
        }

        public void Send(Response response)
        {
            var json = JsonConvert.SerializeObject(response);
            this.Send(json);
        }

        public bool SendFlip(FlipInstance flip)
        {
            if (Settings != null && Settings.MatchesSettings(flip) && !flip.Sold)
                SendMessage(GetFlipMsg(flip), "/viewauction " + flip.Uuid, string.Join('\n', flip.Interesting.Select(s => "・" + s)));
            return true;
        }

        private string GetFlipMsg(FlipInstance flip)
        {
            return $"FLIP: {GetRarityColor(flip.Rarity)}{flip.Name} §f{string.Format("{0:n0}", flip.LastKnownCost)} -> {string.Format("{0:n0}", flip.MedianPrice)} §g[BUY]";
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
                Tier.MYTHIC => "§d",
                _ => ""
            };
        }

        public bool SendSold(string uuid)
        {
            // don't send extra messages
            return true;
        }

        public void UpdateSettings(SettingsChange settings)
        {
            if (this.Settings == DEFAULT_SETTINGS)
                SendMessage($"Authorized connection you can now control settings via the website", "/say whoop");
            else
                SendMessage($"settings changed", "/say whoop");
            Settings = settings.Settings;

            if (settings.Tier.HasFlag(AccountTier.PREMIUM))
                FlipperService.Instance.AddConnection(this);
            else
                FlipperService.Instance.AddNonConnection(this);
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