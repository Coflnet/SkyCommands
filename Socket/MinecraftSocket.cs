using System;
using Coflnet.Sky.Filter;
using hypixel;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Commands
{
    public class MinecraftSocket : WebSocketBehavior, IFlipConection
    {
        public string Uuid;

        public long Id => Uuid.GetHashCode();

        public FlipSettings Settings { get; set; }

        protected override void OnOpen()
        {
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            if (args["uuid"] == null)
                Send(Response.Create("error", "the connection query string needs to include uuid"));

            Uuid = args["uuid"];
            Console.WriteLine(Uuid);
            var key = new Random().Next();
            base.OnOpen();
            SendMessage("Please click this [LINK] to login", $"https://sky.coflnet.com/conMc?uuid={Uuid}&secret={key % 100000}");
            FlipperService.Instance.AddNonConnection(this);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
            Console.WriteLine("received message from mcmod " + e.Data);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            FlipperService.Instance.RemoveConnection(this);
        }

        public void SendMessage(string text, string clickAction)
        {
            this.Send(Response.Create("writeToChat", new { text, onClick = clickAction }));
        }

        public void Send(Response response)
        {
            var json = JsonConvert.SerializeObject(response);
            this.Send(json);
        }

        public bool SendFlip(FlipInstance flip)
        {
            if (!Settings.MatchesSettings(flip))
                return true;
            if (flip.MedianPrice - flip.LastKnownCost < 10_000 && flip.Bin)
                SendMessage(GetFlipMsg(flip), "/viewauction " + flip.Uuid);
            return true;
        }

        private string GetFlipMsg(FlipInstance flip)
        {
            return $"FLIP: {GetRarityColor(flip.Rarity)}{flip.Name} §f{flip.LastKnownCost} -> {flip.MedianPrice} §g[BUY]";
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
            throw new NotImplementedException();
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