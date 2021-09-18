using System;
using System.IO;
using System.Text;
using WebSocketSharp.Net;
using System.Threading.Tasks;

namespace hypixel
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
        public static async Task WriteEnd(
            this RequestContext response, string stringContent
        )
        {
            await response.WriteAsync(stringContent + "</body></html>");
            //response.Close();

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
            //res.Close();
            return url;
        }
    }
}
