using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using RestSharp;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;

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
        public PreviewService(IConfiguration config)
        {
            this.config = config;
            skyClient = new RestClient(config["SKY_BASE_URL"] ?? "https://sky.coflnet.com");
            skyCryptClient = new RestClient(config["SKYCRYPT_BASE_URL"] ?? "https://skycrypt.coflnet.com");
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
        /// <param name="size">the size to get the image in</param>
        /// <returns></returns>
        public async Task<Preview> GetItemPreview(string tag, int size = 32)
        {
            var request = new RestRequest("/item/{tag}").AddUrlSegment("tag", tag);

            /* Most icons are currently available via the texture pack
            if(details.MinecraftType.StartsWith("Leather "))
                request = new RestRequest("/leather/{type}/{color}")
                    .AddUrlSegment("type", details.MinecraftType.Replace("Leather ","").ToLower())
                    .AddUrlSegment("color", details.color.Replace(":",",")); */

            var uri = skyCryptClient.BuildUri(request);
            var response = await GetProxied(uri, size);
            var hash = Encoding.UTF8.GetString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(tag)));
            var brokenFilehash = new HashSet<string>() { "1mfgd8A3YEGnfidqz4q0xg==" };

            if (hash == "��D`��9��U�\u0006�/�o" && !uri.Authority.Contains("coflnet"))
            {
                // try to get image from own instance
                var coflSkyCryptClient = new RestClient("https://skycrypt.coflnet.com");
                uri = coflSkyCryptClient.BuildUri(request);
                response = await GetProxied(uri, size);
                Console.WriteLine($"retrying with coflnet {uri} {response.StatusCode}");
            }
            var fileHashBase64 = Convert.ToBase64String(MD5.Create().ComputeHash(response.RawBytes));
            Items.Client.Model.Item details = null;
            if (response.StatusCode != System.Net.HttpStatusCode.OK || brokenFilehash.Contains(fileHashBase64))
            {
                var isPet = tag.StartsWith("PET_");
                if (!isPet)
                    dev.Logger.Instance.Error($"Failed to load item preview for {tag} from {uri} code {response.StatusCode}");
                var info = await DiHandler.GetService<Items.Client.Api.IItemsApi>().ItemItemTagGetWithHttpInfoAsync(tag, true);
                Console.WriteLine($"info {info.StatusCode}");
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
                if (details?.IconUrl == null && !isPet)
                {
                    Console.WriteLine($"retrieving from api");
                    url = await GetIconUrl(tag);
                };
                if (url.StartsWith("https://sky.coflnet.com") && url.Length >= ("https://sky.coflnet.com/static/icon/" + tag).Length)
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
                Console.WriteLine($"response for {tag} {response.StatusCode} {response.RawBytes?.Length}");
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

        private async Task<string> GetIconUrl(string tag)
        {
            string url;
            var itemDataString = await hypixelClient.ExecuteAsync(new RestRequest("resources/skyblock/items"));
            var itemData = JsonConvert.DeserializeObject<HypixelItems>(itemDataString.Content);
            var targetItem = itemData.Items.Where(i => i.Id == tag).FirstOrDefault();
            Console.Write(JsonConvert.SerializeObject(itemData).Truncate(200));
            if (targetItem == null && tag.StartsWith("POTION_"))
                return skyCryptClient.BuildUri(new RestRequest("/item/POTION")).ToString();
            if (targetItem == null)
                throw new CoflnetException("unkown_item", "there was no image found for the item " + tag);
            var skycryptBase = config["SKYCRYPT_BASE_URL"];
            if (targetItem.Material == "SKULL_ITEM")
            {
                dynamic skinData = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(targetItem.Skin)));
                url = skycryptBase + "/head/" + ((string)skinData.textures.SKIN.url).Replace("http://textures.minecraft.net/texture/", "");
                Activity.Current?.AddTag("headUrl", url);
            }
            else if (targetItem.Material == "INK_SACK")
            {
                url = $"{skycryptBase}/item/{targetItem.Material}:{targetItem.Durability}";
            }
            else
            {
                url = skycryptBase + "/item/" + targetItem.Material;
            }
            Console.WriteLine(url);

            return url;
        }

        private async Task<RestResponse> GetProxied(Uri uri, int size)
        {
            // request image to be squared
            var proxyRequest = new RestRequest($"/a/rs:fill:{size}:{size}/plain/" + uri.ToString())
                        .AddUrlSegment("size", size);
            proxyRequest.Timeout = 5000;
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
            public string Skin { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }
        }
    }
}