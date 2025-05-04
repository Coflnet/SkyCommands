using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.ModCommands.Client.Api;
using MessagePack;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using WebSocketSharp;

namespace Coflnet.Sky.Commands;
public class VpsLogCommand : Command
{
    public override async Task Execute(MessageData data)
    {
        var userId = data.UserId;
        var vpsController = data.GetService<IVpsApi>();
        var instanceResponse = await vpsController.VpsInstancesGetAsync(userId.ToString());
        if (!instanceResponse.TryOk(out var instances))
        {
            await data.SendBack(data.Create("error", "Unable to get vps instances"));
            return;
        }
        if(instances.Count == 0)
        {
            await data.SendBack(data.Create("error", "No vps instances found, go start one"));
            return;
        }
        var target = instances.First().Id;
        var configuration = data.GetService<IConfiguration>();
        var url = configuration["LOKI_BASE_URL"].Replace("http:", "ws:") + "/loki/api/v1/tail";
        var query = $"{{container=\"tpm-manager\", instance_id=\"{target}\"}}";
        var startTime = DateTimeOffset.UtcNow;
        var nanoSeconds = (startTime - TimeSpan.FromHours(1)).ToUnixTimeMilliseconds() * 1_000_000;
        var fullUrl = $"{url}?query={Uri.EscapeDataString(query)}&start={nanoSeconds}&limit=100";
        var ws = new WebSocket(fullUrl);

        ws.OnMessage += (sender, e) =>
        {
            if (!e.IsBinary)
            {
                return;
            }
            var logEntry = JsonConvert.DeserializeObject<LogStreamResponse>(e.Data);

            data.SendBack(data.Create("logLines", logEntry.streams.SelectMany(x => x.values).Select(x =>
            {
                var time = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(x[0]) / 1000000);
                return new LogLine
                {
                    Text = x[1],
                    Time = time,
                    Instanceid = target?.ToString()
                };
            }))).Wait();
        };

        var source = new TaskCompletionSource<bool>();
        ws.OnError += (sender, e) =>
        {
            data.LogError(e.Exception, "Unable to connect to Loki");
            data.SendBack(data.Create("error", "Unable to connect to Loki"));
            source.SetResult(true);
        };
        ws.OnClose += (sender, e) =>
        {
            source.SetResult(true);
        };
        (data as SocketMessageData).Connection.OnBeforeClose += (e) =>
        {
            ws?.Close();
        };
        ws.Connect();
        await source.Task;
    }


    public class LogStreamResponse
    {
        public List<LogStream> streams { get; set; } = new();
    }

    public class LogStream
    {
        public Dictionary<string, string> stream { get; set; } = new();
        public List<string[]> values { get; set; } = new();
    }

    [MessagePackObject]
    public class LogLine
    {
        [Key("text")]
        public string Text { get; set; }
        [Key("time")]
        public DateTimeOffset Time { get; set; }
        [Key("instanceid")]
        public string Instanceid { get; set; }
    }
}
