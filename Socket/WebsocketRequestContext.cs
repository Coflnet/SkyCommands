using WebSocketSharp.Server;
using WebSocketSharp;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace hypixel
{
    public class WebsocketRequestContext : RequestContext
        {
            public HttpRequestEventArgs original;

            public WebsocketRequestContext(HttpRequestEventArgs original, OpenTracing.ISpan span)
            {
                this.original = original;
                this.original.Response.SendChunked = true;
                this.Span = span;
            }

            public override string HostName => original.Request.UserHostName;

            public override IDictionary<string, string> QueryString => (IDictionary<string, string>)original.Request.QueryString;

            public override string path => original.Request.RawUrl;

            public override string UserAgent => original.Request?.UserAgent;

            public override void AddHeader(string name, string value)
            {
                original.Response.AppendHeader(name, value);
            }

            public override void Redirect(string uri)
            {
                original.Response.Redirect(uri);

            }

            public override void SetContentType(string type)
            {
                original.Response.ContentType = type;
            }

            public override void SetStatusCode(int code)
            {
                original.Response.StatusCode = code;
            }

            public override Task WriteAsync(string data)
            {
                return original.Response.WritePartial(data);
            }

            public override void WriteAsync(byte[] data)
            {
                original.Response.WriteContent(data);
            }

            public override void ForceSend(bool finish = false)
            {
                original.Response.OutputStream.Flush();
                if (finish)
                    original.Response.Close();
            }
        }
}
