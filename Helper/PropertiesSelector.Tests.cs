using System.Linq;
using NUnit.Framework;

namespace Coflnet.Sky.Commands.Helper
{
    public class PropertiesSelectorTests
    {
        [Test]
        public void DragonHunter()
        {
            var auction = new hypixel.SaveAuction()
            {
                Enchantments = new System.Collections.Generic.List<hypixel.Enchantment>() {
                    new hypixel.Enchantment(hypixel.Enchantment.EnchantmentType.dragon_hunter, 5)
                    }
            };
            var prop = PropertiesSelector.GetProperties(auction).Select(p=>p.Value).First();
            Assert.AreEqual("Dragon Hunter: 5",prop);
        }
    }
}