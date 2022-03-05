using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using hypixel;
using Newtonsoft.Json;
using RestSharp;

namespace Coflnet.Sky.Commands.Services
{
    public class PreviewService
    {
        public static PreviewService Instance;
        private RestClient crafatarClient = new RestClient("https://crafatar.com");
        private RestClient skyLeaClient = new RestClient("https://sky.shiiyu.moe");
        private RestClient skyClient = new RestClient("https://sky.coflnet.com");
        private RestClient proxyClient = new RestClient("http://imgproxy");
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

        public async Task<Preview> GetItemPreview(string tag, int size = 32)
        {
            var request = new RestRequest("/item/{tag}").AddUrlSegment("tag", tag);

            var details = await ItemDetails.Instance.GetDetailsWithCache(tag);
            /* Most icons are currently available via the texture pack
            if(details.MinecraftType.StartsWith("Leather "))
                request = new RestRequest("/leather/{type}/{color}")
                    .AddUrlSegment("type", details.MinecraftType.Replace("Leather ","").ToLower())
                    .AddUrlSegment("color", details.color.Replace(":",",")); */

            var uri = skyLeaClient.BuildUri(request);
            IRestResponse response = await GetProxied(uri, size);



            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // mc-heads has issues currently
                var url = details.IconUrl;
                if (details.IconUrl == null)
                {
                    url = await GetIconUrl(tag);
                };
                uri = skyClient.BuildUri(new RestRequest(url));
                response = await GetProxied(uri, size);
            }

            return new Preview()
            {
                Id = tag,
                Image = response.RawBytes == null ? null : Convert.ToBase64String(response.RawBytes),
                ImageUrl = uri.ToString(),
                Name = details.Names.FirstOrDefault(),
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
            if (targetItem.Material == "SKULL_ITEM")
            {
                dynamic skinData = JsonConvert.DeserializeObject(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(targetItem.Skin)));
                url = "https://sky.shiiyu.moe/head/" + ((string)skinData.textures.SKIN.url).Replace("http://textures.minecraft.net/texture/","");
            }
            else if (targetItem.Material == "INK_SACK")
            {
                url = $"https://sky.shiiyu.moe/item/{targetItem.Material}:{targetItem.Durability}";
            }
            else
            {
                url = "https://sky.shiiyu.moe/item/" + targetItem.Material;
            }
            Console.WriteLine(url);

            return url;
        }

        private async Task<IRestResponse> GetProxied(Uri uri, int size)
        {
            var proxyRequest = new RestRequest($"/x{size}/" + uri.ToString())
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

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("furniture")]
            public string Furniture { get; set; }

            [JsonProperty("tier")]
            public string Tier { get; set; }

            [JsonProperty("museum")]
            public bool Museum { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("generator")]
            public string Generator { get; set; }

            [JsonProperty("generator_tier")]
            public int? GeneratorTier { get; set; }

            [JsonProperty("glowing")]
            public bool? Glowing { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("stats")]
            public Stats Stats { get; set; }

            [JsonProperty("npc_sell_price")]
            public float? NpcSellPrice { get; set; }

            [JsonProperty("unstackable")]
            public bool? Unstackable { get; set; }

            [JsonProperty("color")]
            public string Color { get; set; }

            [JsonProperty("dungeon_item")]
            public bool? DungeonItem { get; set; }

            [JsonProperty("ability_damage_scaling")]
            public double? AbilityDamageScaling { get; set; }
        }
    }
}