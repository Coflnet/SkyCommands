
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
                Bin = false
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


    }
}