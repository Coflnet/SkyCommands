using System;
using System.IO;
using System.Text;
using WebSocketSharp.Net;
using System.Threading.Tasks;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public static class ResponseExtentions
    {
        public static async Task WritePartial(
            this HttpListenerResponse response, string stringContent
        )
        {
            if (response == null)
                throw new ArgumentNullException("response");

            if (stringContent == null)
                throw new ArgumentNullException("content");

            var content = Encoding.UTF8.GetBytes(stringContent);

            var len = content.LongLength;
            if (len == 0)
            {
                return;
            }

            var output = response.OutputStream;

            if (len <= Int32.MaxValue)
                await output.WriteAsync(content, 0, (int)len);
            else
                output.WriteBytes(content, 1024);
        }

        public static void WriteContent(
            this HttpListenerResponse response, byte[] content
        )
        {
            if (response == null)
                throw new ArgumentNullException("response");

            if (content == null)
                throw new ArgumentNullException("content");

            var len = content.LongLength;
            if (len == 0)
            {
                response.Close();
                return;
            }

            response.ContentLength64 = len;

            var output = response.OutputStream;

            if (len <= Int32.MaxValue)
                output.Write(content, 0, (int)len);
            else
                output.WriteBytes(content, 1024);

            output.Close();
        }
        public static async Task WriteEnd(
            this RequestContext response, string stringContent
        )
        {
            await response.WriteAsync(stringContent + "</body></html>");
        }

        internal static void WriteBytes(
            this Stream stream, byte[] bytes, int bufferLength
            )
        {
            using (var src = new MemoryStream(bytes))
                src.CopyTo(stream, bufferLength);
        }


        public static string RedirectSkyblock(this RequestContext res, string parameter = null, string type = null, string seoTerm = null)
        {
            var url = $"https://sky.coflnet.com" + (type == null ? "" : $"/{type}") + (parameter == null ? "" : $"/{parameter}") + (seoTerm == null ? "" : $"/{seoTerm}");
            res.Redirect(url);
            return url;
        }
    }
}
