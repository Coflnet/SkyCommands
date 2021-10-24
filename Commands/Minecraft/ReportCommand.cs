using System.Threading.Tasks;
using hypixel;
using Newtonsoft.Json;
using OpenTracing.Util;

namespace Coflnet.Sky.Commands.MC
{
    public class ReportCommand : McCommand
    {
        public override Task Execute(MinecraftSocket socket, string arguments)
        {
            using var reportSpan = socket.tracer.BuildSpan("report")
                        .WithTag("message", arguments.Truncate(150))
                        .WithTag("error", "true")
                        .WithTag("settings", JsonConvert.SerializeObject(socket.Settings))
                        .AsChildOf(socket.ConSpan).StartActive();
            var spanId = reportSpan.Span.Context.SpanId.Truncate(6);
            reportSpan.Span.SetTag("id", spanId);

            socket.SendMessage(COFLNET + "Thanks for your report :)\n If you need further help, please refer to this report with " + spanId, spanId);
            return Task.CompletedTask;
        }
    }
}