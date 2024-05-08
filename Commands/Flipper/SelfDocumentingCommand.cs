using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands;

public abstract class SelfDocumentingCommand<Tin, Tout> : Command
{
    public override async Task Execute(MessageData data)
    {
        var typed = new TypedMessageData(data as SocketMessageData ?? throw new CoflnetException("socket_only", "This command is only available via the websocket"));
        var result = await Execute(typed);
        if (typeof(Tout) == typeof(Void))
        {
            await typed.Ok();
            return;
        }
        await typed.SendBack(typed.Create("result", result));
    }

    protected abstract Task<Tout> Execute(TypedMessageData data);

    public class TypedMessageData : SocketMessageData
    {
        public TypedMessageData(SocketMessageData data)
        {
            this.Span = data.Span;
            this.Connection = data.Connection;
            this.Data = data.Data;
            this.mId = data.mId;
            this.Type = data.Type;
            this.MaxAge = data.MaxAge;
        }

        public Tin Get()
        {
            return GetAs<Tin>();
        }
    }

}
