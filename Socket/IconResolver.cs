using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Finds icons for given query
    /// </summary>
    public class IconResolver
    {
        public static IconResolver Instance { get; }

        static IconResolver()
        {
            Instance = new IconResolver();
        }

        /// <summary>
        /// Find and returns item icon
        /// </summary>
        /// <param name="context"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task Resolve(RequestContext context, string path)
        {
            var parts = path.Split("/");
            var tag = parts.Last();
            var isVanilla = false;
            if(tag == "vanilla")
            {
                tag = parts.Reverse().Skip(1).First();
                isVanilla = true;
            }
            Console.WriteLine("Resolving icon for " + tag);
            var key = "img" + tag;
            PreviewService.Preview preview = null;// await CacheService.Instance.GetFromRedis<PreviewService.Preview>(key);
            var cacheTime = TimeSpan.FromDays(0.1);
            Task save = null;
            if (preview == null)
            {
                if (ItemDetails.Instance.GetItemIdForTag(tag, false) <= 0)
                {
                    if (!ItemDetails.Instance.TagLookup.ContainsKey("PET_SKIN_" + tag))
                        throw new CoflnetException("unkown_item", "The requested item was not found, please file a bugreport");
                    else
                        tag = "PET_SKIN_" + tag;
                }
                preview = await DiHandler.ServiceProvider.GetRequiredService<PreviewService>().GetItemPreview(tag, isVanilla, 64);
                if (preview.Image == "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo=" || preview.Image == null || preview.Image.Length < 50)
                {
                    // transparent 64x64 image
                    preview.Image = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAQAAAAAYLlVAAAAOUlEQVR42u3OIQEAAAACIP1/2hkWWEBzVgEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAYF3YDicAEE8VTiYAAAAAElFTkSuQmCC";
                    preview.MimeType = "image/png";
                    cacheTime = TimeSpan.FromSeconds(1);
                    Console.WriteLine("No image found for " + tag);
                    TrackingService.Instance.TrackPage("https://error" + path, "not found/" + tag, null, null);
                }
                save = CacheService.Instance.SaveInRedis<PreviewService.Preview>(key, preview, cacheTime);
            }
            context.SetContentType(preview.MimeType);
            if (cacheTime > TimeSpan.FromHours(1))
                context.AddHeader("cache-control", $"public,max-age={3600 * 24 * 365}");
            else
                context.AddHeader("cache-control", $"public,max-age=120");
            await context.WriteAsync(Convert.FromBase64String(preview.Image));
            if (save != null)
                await save;
        }
    }
}
