using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Coflnet.Sky.Commands.Services
{
    public class PreviewService
    {
        private RestClient crafatarClient;
        private RestClient skyCryptClient;
        private RestClient skyClient;
        private RestClient proxyClient;
        private RestClient hypixelClient;
        private IConfiguration config;
        /// <summary>
        /// Maps old 1.8 Minecraft material names to their modern 1.21 equivalents.
        /// Some materials returned by the Hypixel API no longer exist in newer versions.
        /// </summary>
        private static readonly Dictionary<string, string> materialMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "BED", "RED_BED" },
            { "BREWING_STAND_ITEM", "BREWING_STAND" },
            { "COMMAND", "COMMAND_BLOCK" },
            { "ENDER_PORTAL_FRAME", "END_PORTAL_FRAME" },
            { "FIREBALL", "FIRE_CHARGE" },
            { "FIREWORK_CHARGE", "FIREWORK_STAR" },
            { "FLOWER_POT_ITEM", "FLOWER_POT" },
            { "LONG_GRASS", "SHORT_GRASS" },
            { "MONSTER_EGG", "ZOMBIE_SPAWN_EGG" },
            { "NETHER_BRICK_ITEM", "NETHER_BRICK" },
            { "QUARTZ_ORE", "NETHER_QUARTZ_ORE" },
            { "WOOD_BUTTON", "OAK_BUTTON" },
        };
        private static readonly Dictionary<string, string> iconOverrides = new Dictionary<string, string>
        {
            { "ESSENCE_FOREST", "https://mc-heads.net/head/e019ece125ec79f95178e1af4da2a8b982de71ed42c31cac4b1d2f665355df5a/64" },
            { "ESSENCE_SAFARI", "https://mc-heads.net/head/2d9eb19536a482fdfdba3dcd89dcbbd5daa94180d69e156d052061e376373aea/64" },
            { "ESSENCE_FOSSIL", "https://mc-heads.net/head/93a1b830399ab432a5178fdaf3939b24bf25c724a66be947296c503352bc380d/64" },
        };

        /// <summary>
        /// Our static mirror of skycrypt's (sky.shiiyu.moe) former image paths, copied 1:1 so the
        /// /api/item/{tag}, /api/head/{hash} and /api/leather/{piece}/{rgb} shapes keep working
        /// without depending on the now-defunct skycrypt image endpoints.
        /// </summary>
        public const string DefaultSkycryptBaseUrl = "https://static.coflnet.com/sky/skycrypt";
        /// <summary>
        /// Our fork of crafatar, deployed in-cluster, used to render player skull heads directly
        /// instead of going through skycrypt.
        /// </summary>
        public const string DefaultHeadRenderBaseUrl = "http://crafatar:3000";
        private const string OwnIconPrefix = "https://sky.coflnet.com/static/icon/";

        public PreviewService(IConfiguration config)
        {
            this.config = config;
            skyClient = new RestClient(config["SKY_BASE_URL"] ?? "https://sky.coflnet.com");
            skyCryptClient = new RestClient(config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl);
            crafatarClient = new RestClient(config["CRAFATAR_BASE_URL"] ?? "https://crafatar.com");
            proxyClient = new RestClient(config["IMGPROXY_BASE_URL"] ?? "http://imgproxy");
            hypixelClient = new RestClient(config["HYPIXEL_BASE_URL"] ?? "https://api.hypixel.net/");
        }

        public async Task<Preview> GetPlayerPreview(string id)
        {
            var request = new RestRequest("/avatars/{uuid}").AddUrlSegment("uuid", id).AddQueryParameter("overlay", "");

            var uri = crafatarClient.BuildUri(request.AddParameter("size", 64));
            var response = await crafatarClient.ExecuteAsync(request.AddParameter("size", 8));

            return new Preview()
            {
                Id = id,
                Image = response.RawBytes == null ? null : Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
                Name = await Shared.DiHandler.GetService<PlayerName.PlayerNameService>()
                    .GetName(id)
            };
        }

        /// <summary>
        /// Gets image preview for an item
        /// </summary>
        /// <param name="tag">The hypixel item tag to get an image for</param>
        /// <param name="isVanilla"></param>
        /// <param name="size">the size to get the image in</param>
        /// <returns></returns>
        public async Task<Preview> GetItemPreview(string tag, bool isVanilla, int size = 32)
        {
            if (tag.StartsWith("ENCHANTMENT_"))
                tag = "ENCHANTED_BOOK";
            var request = new RestRequest("/api/item/{tag}").AddUrlSegment("tag", tag);

            var uri = skyCryptClient.BuildUri(request);
            var response = await GetProxied(uri, size);
            var brokenFilehash = new HashSet<string>() { "1mfgd8A3YEGnfidqz4q0xg==", null, "vbFna5G5td5ICdFlwkA97A==", "8pPWnpUQWNjGqrCtu0KXoQ==", "QYYMg/ZsR4QjWxtViNiRSA==", "FPQFp8YkfQBwmAiwBxPTrg==" };
            var fileHashBase64 = GetResponseHash(response);
            Items.Client.Model.Item details = null;
            if (response.StatusCode != System.Net.HttpStatusCode.OK || brokenFilehash.Contains(fileHashBase64) || isVanilla)
            {
                if (!NBT.IsPet(tag) && !isVanilla)
                    dev.Logger.Instance.Error($"Failed to load item preview for {tag} from {uri} code {response.StatusCode}");
                var info = await DiHandler.GetService<Items.Client.Api.IItemsApi>().ItemItemTagGetWithHttpInfoAsync(tag, true);
                Console.WriteLine($"info {info.StatusCode} {info.RawContent}");
                if (info.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    details = info.Data;
                    if (info.Data == null) // parse manually to find json issues
                        details = JsonConvert.DeserializeObject<Items.Client.Model.Item>(info.RawContent);
                }
                else
                {
                    Console.WriteLine($"failed to load item details for {tag} from api");
                }
                var url = details?.IconUrl;
                // our own icon url is the non-vanilla one, vanilla requires the minecraft material.
                // it can also be a leftover/circular url for skull items whose /api/item/{tag} was
                // never in skycrypt (heads were only mirrored by texture hash) - resolve those via
                // the Hypixel API (GetIconUrl) too, regardless of the vanilla flag, instead of
                // returning the "image unobtainable (loop)" preview below.
                if (ShouldResolveViaHypixelApi(url, NBT.IsPet(tag)))
                {
                    Console.WriteLine($"retrieving from api");
                    url = await GetIconUrl(tag);
                }
                if (url == null)
                    return new Preview() { Id = tag, Name = details?.Name };
                if (url.StartsWith("https://texture"))
                {
                    url = ConvertTextureUrlToSkull(config["HEAD_RENDER_BASE_URL"] ?? DefaultHeadRenderBaseUrl, url);
                }
                // keep the loop guard for anything that would still point back at our own /static/icon
                if (IsOwnIconUrl(url) && url.Length >= (OwnIconPrefix + tag).Length && !isVanilla)
                {
                    Console.WriteLine($"skipping loop {url}");
                    return new Preview()
                    {
                        Id = tag,
                        Name = "image unobtainable (loop)",
                    };
                }
                uri = skyClient.BuildUri(new RestRequest(url));
                Console.WriteLine($"alternate url {url} for {tag}");
                response = await GetProxied(uri, size);
                var hash = GetResponseHash(response);
                if (brokenFilehash.Contains(hash) && url.Contains("mc-heads.net"))
                {
                    var headRenderBase = config["HEAD_RENDER_BASE_URL"] ?? DefaultHeadRenderBaseUrl;
                    var headHash = ExtractMcHeadsHash(url);
                    var renderUrl = BuildHeadRenderUrl(headRenderBase, headHash);
                    uri = skyClient.BuildUri(new RestRequest(renderUrl));
                    Console.WriteLine($"replacing steve head {url} with {uri}");
                    response = await GetProxied(uri, size);
                    hash = GetResponseHash(response);
                    if (brokenFilehash.Contains(hash) || response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        var mirrorUrl = BuildMirrorHeadUrl(config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl, headHash);
                        uri = skyClient.BuildUri(new RestRequest(mirrorUrl));
                        Console.WriteLine($"crafatar render failed, falling back to mirror head {mirrorUrl}");
                        response = await GetProxied(uri, size);
                        hash = GetResponseHash(response);
                    }
                }
                else if ((brokenFilehash.Contains(hash) || response.StatusCode != System.Net.HttpStatusCode.OK) && url.Contains("/renders/head/"))
                {
                    // our own crafatar render failed / returned a broken hash, fall back to the static mirror
                    var headHash = ExtractHeadHashFromRenderUrl(url);
                    var mirrorUrl = BuildMirrorHeadUrl(config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl, headHash);
                    uri = skyClient.BuildUri(new RestRequest(mirrorUrl));
                    Console.WriteLine($"crafatar head render failed for {url}, falling back to mirror {mirrorUrl}");
                    response = await GetProxied(uri, size);
                    hash = GetResponseHash(response);
                }
                else if(brokenFilehash.Contains(hash))
                {
                    // convert the 1.8 minecraft type to 1.21
                    var materialPart = url.Split('/').Last();
                    var mapped = MapMaterial(materialPart);
                    if (mapped != materialPart)
                    {
                        var skycryptBase = config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl;
                        var mappedUrl = skycryptBase + "/api/item/" + mapped;
                        uri = skyClient.BuildUri(new RestRequest(mappedUrl));
                        Console.WriteLine($"remapping old material {materialPart} to {mapped} for {tag}");
                        response = await GetProxied(uri, size);
                        hash = GetResponseHash(response);
                    }
                }
                if (brokenFilehash.Contains(hash) && IsSkycryptMirrorUrl(url, config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl))
                {
                    var material = url.Split('/').Last();
                    if (material != tag)
                    {
                        uri = skyClient.BuildUri(new RestRequest("/static/icon/" + material));
                        Console.WriteLine($"replacing broken mirror image {url} with {uri}");
                        response = await GetProxied(uri, size);
                        hash = GetResponseHash(response);
                    }
                }
                Console.WriteLine($"response for {tag} {response.StatusCode} {response.RawBytes?.Length} {hash} {url}");
            }

            return new Preview()
            {
                Id = tag,
                Image = response?.RawBytes == null ? null : Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri?.ToString(),
                Name = details?.Name,
                MimeType = response?.ContentType
            };
        }

        private static string GetResponseHash(RestResponse response)
        {
            return response?.RawBytes == null ? null : Convert.ToBase64String(MD5.Create().ComputeHash(response.RawBytes));
        }

        private async Task<string> GetIconUrl(string tag)
        {
            if (iconOverrides.TryGetValue(tag, out var iconUrl))
                return iconUrl;
            string url;
            var itemDataString = await hypixelClient.ExecuteAsync(new RestRequest("v2/resources/skyblock/items"));
            var itemData = JsonConvert.DeserializeObject<HypixelItems>(itemDataString.Content);
            var targetItem = itemData.Items.Where(i => i.Id == tag).FirstOrDefault();
            Console.Write(JsonConvert.SerializeObject(targetItem).Truncate(200));
            if (targetItem == null && tag.StartsWith("POTION_"))
                return skyCryptClient.BuildUri(new RestRequest("/api/item/POTION")).ToString();
            if (targetItem == null)
                throw new CoflnetException("unkown_item", "there was no image found for the item " + tag);
            var skycryptBase = config["SKYCRYPT_BASE_URL"] ?? DefaultSkycryptBaseUrl;
            if (targetItem.Material == "SKULL_ITEM")
            {
                dynamic skinData = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(targetItem.Skin.Value)));
                string skinUrl = skinData.textures.SKIN.url;
                url = ConvertTextureUrlToSkull(config["HEAD_RENDER_BASE_URL"] ?? DefaultHeadRenderBaseUrl, skinUrl);
            }
            else if (targetItem.Material == "INK_SACK")
            {
                url = $"{skycryptBase}/api/item/{targetItem.Material}:{targetItem.Durability}";
            }
            else
            {
                var material = MapMaterial(targetItem.Material);
                url = skycryptBase + "/api/item/" + material;
            }
            Console.WriteLine("final url " + url);

            return url;
        }

        /// <summary>
        /// Builds a render url for our crafatar fork from a textures.minecraft.net skin url (or a
        /// bare texture hash). This is the primary way we get skull head images now; callers should
        /// fall back to <see cref="BuildMirrorHeadUrl"/> if the render comes back broken.
        /// </summary>
        public static string ConvertTextureUrlToSkull(string headRenderBase, string skinUrl)
        {
            string url = BuildHeadRenderUrl(headRenderBase, ExtractTextureHash(skinUrl));
            Activity.Current?.AddTag("headUrl", url);
            return url;
        }

        /// <summary>
        /// Strips the textures.minecraft.net (http or https) prefix off a skin url, leaving the bare
        /// texture hash. If the input is already a bare hash it is returned unchanged.
        /// </summary>
        public static string ExtractTextureHash(string skinUrlOrHash)
        {
            return skinUrlOrHash
                .Replace("http://textures.minecraft.net/texture/", "")
                .Replace("https://textures.minecraft.net/texture/", "");
        }

        /// <summary>
        /// Builds a head render url against our in-cluster crafatar fork.
        /// </summary>
        public static string BuildHeadRenderUrl(string headRenderBase, string textureHash)
        {
            return $"{headRenderBase}/renders/head/{textureHash}?overlay";
        }

        /// <summary>
        /// Builds a fallback head url against the static skycrypt mirror, for when the crafatar
        /// render fails or comes back broken.
        /// </summary>
        public static string BuildMirrorHeadUrl(string skycryptBase, string textureHash)
        {
            return $"{skycryptBase}/api/head/{textureHash}";
        }

        /// <summary>
        /// Pulls the texture hash back out of a url built by <see cref="BuildHeadRenderUrl"/>.
        /// </summary>
        public static string ExtractHeadHashFromRenderUrl(string renderUrl)
        {
            var afterMarker = renderUrl.Split("/renders/head/").Last();
            return afterMarker.Split('?')[0];
        }

        /// <summary>
        /// Extracts the texture hash out of a legacy mc-heads.net head url (e.g. one of the hardcoded
        /// icon overrides).
        /// </summary>
        public static string ExtractMcHeadsHash(string mcHeadsUrl)
        {
            return mcHeadsUrl.Replace("https://mc-heads.net/head/", "").Split('/')[0];
        }

        /// <summary>
        /// Converts one of the hardcoded mc-heads.net icon overrides into a crafatar render url.
        /// </summary>
        public static string ConvertMcHeadsUrlToRenderUrl(string headRenderBase, string mcHeadsUrl)
        {
            return BuildHeadRenderUrl(headRenderBase, ExtractMcHeadsHash(mcHeadsUrl));
        }

        /// <summary>
        /// Whether the given icon url is our own generated static icon (i.e. resolving it further
        /// via the skycrypt mirror/Hypixel API would just call back into ourselves).
        /// </summary>
        public static bool IsOwnIconUrl(string url)
        {
            return url?.StartsWith(OwnIconPrefix) ?? false;
        }

        /// <summary>
        /// Whether the item's icon should be resolved via the Hypixel API (material/skull lookup)
        /// instead of trusting the details' IconUrl as-is: either there is no icon at all, or the
        /// icon is our own static icon pointing back at ourselves (a stale/circular value - the mirror
        /// never had this tag, e.g. a skull item saved by texture hash rather than tag). Pets are
        /// excluded since their preview is generated separately.
        /// </summary>
        public static bool ShouldResolveViaHypixelApi(string iconUrl, bool isPet)
        {
            if (isPet)
                return false;
            return iconUrl == null || IsOwnIconUrl(iconUrl);
        }

        /// <summary>
        /// Whether the given (now broken) url points at our static skycrypt mirror - either by the
        /// configured base url, or one of the old dead hosts it replaced.
        /// </summary>
        public static bool IsSkycryptMirrorUrl(string url, string skycryptBase)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            if (url.Contains("sky.shiiyu.moe") || url.Contains("skycrypt.coflnet.com"))
                return true;
            return !string.IsNullOrEmpty(skycryptBase) && url.StartsWith(skycryptBase);
        }

        /// <summary>
        /// Maps an old 1.8 Minecraft material name to its modern 1.21 equivalent.
        /// Returns the original name if no mapping exists.
        /// </summary>
        private static string MapMaterial(string material)
        {
            // strip durability suffix for lookup (e.g. "INK_SACK:4" -> "INK_SACK")
            var baseMaterial = material.Contains(':') ? material.Split(':')[0] : material;
            if (materialMappings.TryGetValue(baseMaterial, out var mapped))
                return material.Contains(':') ? mapped + ":" + material.Split(':')[1] : mapped;
            return material;
        }

        private async Task<RestResponse> GetProxied(Uri uri, int size)
        {
            // request image to be squared
            var proxyRequest = new RestRequest(BuildProxyPath(uri, size));
            proxyRequest.Timeout = TimeSpan.FromSeconds(5);
            var response = await proxyClient.ExecuteAsync(proxyRequest);
            return response;
        }

        /// <summary>
        /// Builds the imgproxy path for a source url. The source has to be escaped,
        /// otherwise imgproxy treats its query (eg. crafatar's ?overlay) as its own and drops it.
        /// </summary>
        public static string BuildProxyPath(Uri source, int size)
        {
            return $"/a/rs:fill:{size}:{size}/plain/" + Uri.EscapeDataString(source.ToString());
        }

        [DataContract]
        public class Preview
        {
            [DataMember(Name = "id")]
            public string Id;
            [DataMember(Name = "img")]
            public string Image;
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "imgUrl")]
            public string ImageUrl;
            [DataMember(Name = "mime")]
            public string MimeType;
        }

        public class HypixelItems
        {
            public List<ItemData> Items { get; set; }
        }

        public class ItemData
        {
            [JsonProperty("material")]
            public string Material { get; set; }

            [JsonProperty("durability")]
            public int Durability { get; set; }

            [JsonProperty("skin")]
            public Skin Skin { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }
        }

        public class Skin
        {
            public string Value { get; set; }
        }
    }
}
