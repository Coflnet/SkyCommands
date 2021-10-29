using System.Linq;
using Newtonsoft.Json;
using hypixel;
using System.Collections.Generic;
using Coflnet.Sky.Commands.MC;

namespace Coflnet.Sky.Commands
{
    public class EnchantColorMapper
    {
        public static EnchantColorMapper Instance = new EnchantColorMapper();
        private Dictionary<Enchantment.EnchantmentType, string> Colors = new Dictionary<Enchantment.EnchantmentType, string>();

        public EnchantColorMapper()
        {

        }

        public ColorSaveAuction AddColors(SaveAuction auction)
        {
            var cauction = new ColorSaveAuction(auction);
            if (auction?.Enchantments != null)
                cauction.Enchantments = auction.Enchantments.Select(e => new ColorEnchant(e)).ToList();
            return cauction;
        }

        public class ColorSaveAuction : SaveAuction
        {
            public ColorSaveAuction()
            { }
            public ColorSaveAuction(SaveAuction auction) : base(auction)
            {
            }

            [JsonProperty("enchantments")]
            public new List<ColorEnchant> Enchantments { get; set; }
        }

        public class ColorEnchant : Enchantment
        {
            [JsonProperty("color")]
            public string ColorPrefix { get; set; }

            public ColorEnchant(Enchantment enchantment) : base(enchantment.Type, enchantment.Level, enchantment.ItemType)
            {
                if (enchantment.Type.ToString().StartsWith("ulti"))
                    ColorPrefix = McColorCodes.LIGHT_PURPLE;
                else if (enchantment.Level >= 6 || Constants.RelevantEnchants.Where(e => e.Type == enchantment.Type && enchantment.Level >= e.Level).Any())
                    ColorPrefix = McColorCodes.DARK_PURPLE;
                else
                    ColorPrefix = McColorCodes.BLUE;
            }
        }
    }
}