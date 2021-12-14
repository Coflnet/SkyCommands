
using System.Collections.Generic;
using System.Linq;
using hypixel;
using NUnit.Framework;

namespace Coflnet.Sky.Filter
{
    public class FlipFilterTests
    {
        FlipInstance sampleFlip = new FlipInstance()
        {
            MedianPrice = 10,
            Volume = 10,
            Auction = new SaveAuction()
            {
                Bin = false,
                Enchantments = new List<Enchantment>(){
                    new Enchantment(Enchantment.EnchantmentType.critical,4)
                },
                FlatenedNBT = new Dictionary<string, string>() { { "candy", "3" } }
            }

        };
        [Test]
        public void IsMatch()
        {
            var settings = new FlipSettings()
            {
                BlackList = new List<ListEntry>() { new ListEntry() { filter = new Dictionary<string, string>() { { "Bin", "true" } } } }
            };
            var matches = settings.MatchesSettings(sampleFlip);
            Assert.IsTrue(matches.Item1, "flip should match");
            sampleFlip.Auction.Bin = true;
            Assert.IsFalse(settings.MatchesSettings(sampleFlip).Item1, "flip should not match");
        }


        [Test]
        public void EnchantmentMatch()
        {
            var settings = new FlipSettings()
            {
                BlackList = new List<ListEntry>() { new ListEntry() { filter = new Dictionary<string, string>() { { "Enchantment", "aiming" }, { "EnchantLvl", "1" } } } }
            };
            var matches = settings.MatchesSettings(sampleFlip);
            Assert.IsTrue(matches.Item1, "flip should match");
        }


        [Test]
        public void EnchantmentBlacklistMatch()
        {
            var settings = new FlipSettings()
            {
                BlackList = new List<ListEntry>() { new ListEntry() { filter = new Dictionary<string, string>() { { "Enchantment", "critical" }, { "EnchantLvl", "4" } } } }
            };
            var matches = settings.MatchesSettings(sampleFlip);
            Assert.IsFalse(matches.Item1, "flip should not match");
        }

        [Test]
        public void CandyBlacklistMatch()
        {
            NBT.Instance = new NBTMock();
            sampleFlip.Auction.NBTLookup = new List<NBTLookup>() { new NBTLookup(1, 2) };
            var settings = new FlipSettings()
            {
                BlackList = new List<ListEntry>() { new ListEntry() { filter = new Dictionary<string, string>() { { "Candy", "any" } } } }
            };
            var matches = settings.MatchesSettings(sampleFlip);
            Assert.IsFalse(matches.Item1, "flip should not match");
        }


        class NBTMock : INBT
        {
            public short GetKeyId(string name)
            {
                return 1;
            }

            public int GetValueId(short key, string value)
            {
                return 2;
            }
        }
    }
}