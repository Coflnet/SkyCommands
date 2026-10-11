using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Microsoft.Extensions.Caching.Memory;
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
            tag = Uri.UnescapeDataString(Uri.UnescapeDataString(tag));
            Console.WriteLine("Resolving icon for " + tag);
            var key = "img" + tag;
            PreviewService.Preview preview = null;// await CacheService.Instance.GetFromRedis<PreviewService.Preview>(key);
            var cacheTime = DefaultCacheTime;
            Task save = null;
            if (preview == null)
            {
                if (DiHandler.GetService<ItemDetails>().GetItemIdForTag(tag, false) <= 0)
                {
                    if (!DiHandler.GetService<ItemDetails>().TagLookup.ContainsKey("PET_SKIN_" + tag))
                    {
                        context.SetStatusCode(404);
                        context.SetContentType("text/plain");
                        await context.WriteAsync("The requested item was not found, please file a bugreport");
                        return;
                    }
                    tag = "PET_SKIN_" + tag;
                }
                var previewService = DiHandler.ServiceProvider.GetRequiredService<PreviewService>();
                preview = await GetPreviewWithFallback(previewService.GetItemPreview, tag, isVanilla);
                if (IsMissing(preview))
                {
                    preview = BuildMissingPreview(tag, preview);
                    cacheTime = MissingCacheTime;
                    Console.WriteLine("No image found for " + tag);
                    TrackingService.Instance.TrackPage("https://error" + path, "not found/" + tag, null, null);
                }
                save = CacheService.Instance.SaveInRedis<PreviewService.Preview>(key, preview, cacheTime);
            }
            context.SetContentType(preview.MimeType);
            context.AddHeader("cache-control", CacheControlFor(cacheTime));
            await context.WriteAsync(Convert.FromBase64String(preview.Image));
            if (save != null)
                await save;
        }

        internal static readonly TimeSpan DefaultCacheTime = TimeSpan.FromDays(0.1);
        internal static readonly TimeSpan MissingCacheTime = TimeSpan.FromSeconds(1);
        private const string TransparentPng = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAQAAAAAYLlVAAAAOUlEQVR42u3OIQEAAAACIP1/2hkWWEBzVgEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAYF3YDicAEE8VTiYAAAAAElFTkSuQmCC";

        /// <summary>
        /// Creates a fresh transparent placeholder for a missing icon. Always a new instance so
        /// the cached missing marker is never mutated.
        /// </summary>
        internal static PreviewService.Preview BuildMissingPreview(string tag, PreviewService.Preview missing)
        {
            return new PreviewService.Preview() { Id = tag, Name = missing?.Name, Image = TransparentPng, MimeType = "image/png" };
        }

        internal static string CacheControlFor(TimeSpan cacheTime)
        {
            return cacheTime > TimeSpan.FromHours(1)
                ? $"public,max-age={3600 * 24 * 365}"
                : "public,max-age=120";
        }

        /// <summary>
        /// Loads the preview, falling back to the non-vanilla icon if the vanilla one can't be loaded
        /// </summary>
        public static async Task<PreviewService.Preview> GetPreviewWithFallback(Func<string, bool, int, Task<PreviewService.Preview>> load, string tag, bool isVanilla)
        {
            // assets that don't exist are expected data, remember them so they are
            // neither re-requested nor re-logged on every request. Transient failures are never remembered.
            if (missingAssets.TryGetValue(MissingKey(tag, isVanilla), out bool _))
                return new PreviewService.Preview() { Id = tag };
            if (!isVanilla)
                return Remember(MissingKey(tag, false), await load(tag, false, 64));
            PreviewService.Preview preview = null;
            var vanillaKey = MissingKey(tag, true) + "/failed";
            // whether the vanilla attempt proved there is no vanilla icon
            var vanillaConclusive = missingAssets.TryGetValue(vanillaKey, out bool _);
            if (!vanillaConclusive)
            {
                try
                {
                    preview = await load(tag, true, 64);
                    // a genuine vanilla miss is remembered so the vanilla fetch isn't repeated
                    if (IsMissing(preview))
                    {
                        Remember(vanillaKey, preview);
                        vanillaConclusive = preview != null && preview.Miss != PreviewService.MissKind.Transient;
                    }
                }
                catch (CoflnetException e)
                {
                    // the item api states there is no such item/icon
                    missingAssets.Set(vanillaKey, true, EntryOptions(MissingAssetTtl));
                    vanillaConclusive = true;
                    Console.WriteLine($"Failed to load vanilla icon for {tag}, falling back to non-vanilla {e.Message}");
                }
                catch (Exception e)
                {
                    // timeouts, cancellations and connection errors may recover, retry next request
                    Console.WriteLine($"Failed to load vanilla icon for {tag} (transient), falling back to non-vanilla {e.Message}");
                }
            }
            if (!IsMissing(preview))
                return preview;
            // the non-vanilla result is known already, don't load it again
            if (missingAssets.TryGetValue(MissingKey(tag, false), out bool _))
                return new PreviewService.Preview() { Id = tag };
            var fallback = await load(tag, false, 64);
            // the vanilla request only counts as missing if the vanilla attempt itself was conclusive
            return Remember(vanillaConclusive ? MissingKey(tag, true) : MissingKey(tag, false), fallback);
        }

        /// <summary>How long a conclusively missing asset is remembered</summary>
        public static readonly TimeSpan MissingAssetTtl = TimeSpan.FromHours(1);
        /// <summary>
        /// How long a 404 of the image proxy is remembered. imgproxy answers 404 for failed source
        /// downloads too, so a source outage looks like a missing icon; keep the memory short.
        /// </summary>
        public static readonly TimeSpan ProxyNotFoundTtl = TimeSpan.FromMinutes(5);
        // only markers are stored, never Preview instances (those get mutated by callers)
        private static readonly MemoryCache missingAssets = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 10_000 });
        private static MemoryCacheEntryOptions EntryOptions(TimeSpan ttl) => new MemoryCacheEntryOptions() { Size = 1, AbsoluteExpirationRelativeToNow = ttl };

        private static string MissingKey(string tag, bool isVanilla) => (isVanilla ? "v/" : "n/") + tag;

        private static PreviewService.Preview Remember(string key, PreviewService.Preview preview)
        {
            if (preview == null || !IsMissing(preview) || preview.Miss == PreviewService.MissKind.Transient)
                return preview;
            missingAssets.Set(key, true, EntryOptions(preview.Miss == PreviewService.MissKind.ProxyNotFound ? ProxyNotFoundTtl : MissingAssetTtl));
            return preview;
        }

        /// <summary>
        /// Whether the preview contains no usable image
        /// </summary>
        public static bool IsMissing(PreviewService.Preview preview)
        {
            return preview?.Image == null || preview.Image == "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo=" || preview.Image.Length < 50;
        }
    }
}
