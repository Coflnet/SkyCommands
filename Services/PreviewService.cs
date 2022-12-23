using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using System.Diagnostics;
using Newtonsoft.Json;
using OpenTracing.Util;
using RestSharp;

namespace Coflnet.Sky.Commands.Services
{
    public class PreviewService
    {
        public static PreviewService Instance;
        private RestClient crafatarClient = new RestClient("https://crafatar.com");
        private RestClient skyLeaClient = new RestClient("https://skycrypt.coflnet.com");
        private RestClient skyClient = new RestClient("https://sky.coflnet.com");
        private RestClient proxyClient = new RestClient(SimplerConfig.SConfig.Instance["IMGPROXY_BASE_URL"] ?? "http://imgproxy");
        private RestClient hypixelClient = new RestClient("https://api.hypixel.net/");
        static PreviewService()
        {
            Instance = new PreviewService();
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
                Name = await PlayerSearch.Instance.GetName(id)
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

            var uri = skyLeaClient.BuildUri(request);
            var response = await GetProxied(uri, size);

            Items.Client.Model.Item details = null;
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var isPet = tag.StartsWith("PET_");
                if (!isPet)
                    dev.Logger.Instance.Error($"Failed to load item preview for {tag} from {uri} code {response.StatusCode}");
                details = await DiHandler.GetService<Items.Client.Api.IItemsApi>().ItemItemTagGetAsync(tag, true);
                var url = details.IconUrl;
                if (details.IconUrl == null && !isPet)
                {
                    Console.WriteLine($"retrieving from api");
                    url = await GetIconUrl(tag);
                };
                if (url.StartsWith("https://sky.coflnet.com"))
                {
                    Console.WriteLine($"skipping loop {url}");
                    return new Preview()
                    {
                        Id = tag,
                        Name = "image unobtainable (loop)",
                    };
                }
                Console.WriteLine($"alternate url {url}");
                uri = skyClient.BuildUri(new RestRequest(url));
                response = await GetProxied(uri, size);
            }

            return new Preview()
            {
                Id = tag,
                Image = response?.RawBytes == null ? null : Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
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
            if (targetItem == null)
                throw new CoflnetException("unkown_item", "there was no image found for the item " + tag);
            var skycryptBase = "https://skycrypt.coflnet.com";
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
            var proxyRequest = new RestRequest($"/a/rs:fit:{size}/plain/" + uri.ToString())
                        .AddUrlSegment("size", size);
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