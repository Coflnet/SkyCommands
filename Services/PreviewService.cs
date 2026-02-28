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

        public PreviewService(IConfiguration config)
        {
            this.config = config;
            skyClient = new RestClient(config["SKY_BASE_URL"] ?? "https://sky.coflnet.com");
            skyCryptClient = new RestClient(config["SKYCRYPT_BASE_URL"] ?? "https://sky.shiiyu.moe/");
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
                if (details?.IconUrl == null && !NBT.IsPet(tag))
                {
                    Console.WriteLine($"retrieving from api");
                    url = await GetIconUrl(tag);
                }
                if (url.StartsWith("https://texture"))
                {
                    url = ConvertTextureUrlToSkull(config["SKYCRYPT_BASE_URL"], url);
                }
                if (url.StartsWith("https://sky.coflnet.com") && url.Length >= ("https://sky.coflnet.com/static/icon/" + tag).Length && !isVanilla)
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
                    uri = skyCryptClient.BuildUri(new RestRequest("/api/head/" + url.Replace("https://mc-heads.net/head/", "").Split('/')[0]));
                    Console.WriteLine($"replacing steve head {url} with {uri}");
                    response = await GetProxied(uri, size);
                    hash = GetResponseHash(response);
                } else if(brokenFilehash.Contains(hash))
                {
                    // convert the 1.8 minecraft type to 1.21
                    var materialPart = url.Split('/').Last();
                    var mapped = MapMaterial(materialPart);
                    if (mapped != materialPart)
                    {
                        var skycryptBase = config["SKYCRYPT_BASE_URL"];
                        var mappedUrl = skycryptBase + "/api/item/" + mapped;
                        uri = skyClient.BuildUri(new RestRequest(mappedUrl));
                        Console.WriteLine($"remapping old material {materialPart} to {mapped} for {tag}");
                        response = await GetProxied(uri, size);
                        hash = GetResponseHash(response);
                    }
                }
                if(brokenFilehash.Contains(hash) && url.Contains("sky.shiiyu.moe"))
                {
                    uri = skyClient.BuildUri(new RestRequest("/static/icon/" + url.Split('/').Last()));
                    Console.WriteLine($"replacing broken sky.shiiyu.moe image {url} with {uri}");
                    response = await GetProxied(uri, size);
                    hash = GetResponseHash(response);
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
            string url;
            var itemDataString = await hypixelClient.ExecuteAsync(new RestRequest("v2/resources/skyblock/items"));
            var itemData = JsonConvert.DeserializeObject<HypixelItems>(itemDataString.Content);
            var targetItem = itemData.Items.Where(i => i.Id == tag).FirstOrDefault();
            Console.Write(JsonConvert.SerializeObject(targetItem).Truncate(200));
            if (targetItem == null && tag.StartsWith("POTION_"))
                return skyCryptClient.BuildUri(new RestRequest("/api/item/POTION")).ToString();
            if (targetItem == null)
                throw new CoflnetException("unkown_item", "there was no image found for the item " + tag);
            var skycryptBase = config["SKYCRYPT_BASE_URL"];
            if (targetItem.Material == "SKULL_ITEM")
            {
                dynamic skinData = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(targetItem.Skin.Value)));
                string skinUrl = skinData.textures.SKIN.url;
                url = ConvertTextureUrlToSkull(skycryptBase, skinUrl);
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

        private static string ConvertTextureUrlToSkull(string skycryptBase, string skinUrl)
        {
            string url = skycryptBase + "/api/head/" + skinUrl
                .Replace("http://textures.minecraft.net/texture/", "")
                .Replace("https://textures.minecraft.net/texture/", "");
            Activity.Current?.AddTag("headUrl", url);
            return url;
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
            var proxyRequest = new RestRequest($"/a/rs:fill:{size}:{size}/plain/" + uri.ToString())
                        .AddUrlSegment("size", size);
            proxyRequest.Timeout = TimeSpan.FromSeconds(5);
            var response = await proxyClient.ExecuteAsync(proxyRequest);
            return response;
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