using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Coflnet.Sky.Commands
{
    public class IconResolverTests
    {
        private static readonly string ValidImage = new string('A', 100);

        [Test]
        public async Task VanillaThrowsFallsBackToNonVanilla()
        {
            // FACTION_RABBIT_MOCKTAIL is not in the hypixel item api, vanilla lookup throws
            var preview = await IconResolver.GetPreviewWithFallback((tag, vanilla, size) =>
            {
                if (vanilla)
                    throw new CoflnetException("unkown_item", "there was no image found for the item " + tag);
                return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = ValidImage });
            }, "FACTION_RABBIT_MOCKTAIL", true);

            Assert.That(preview.Image, Is.EqualTo(ValidImage));
        }

        [Test]
        public async Task VanillaEmptyFallsBackToNonVanilla()
        {
            var preview = await IconResolver.GetPreviewWithFallback((tag, vanilla, size) =>
                Task.FromResult(new PreviewService.Preview() { Id = tag, Image = vanilla ? "tiny" : ValidImage }),
                "SORROW_HELMET", true);

            Assert.That(preview.Image, Is.EqualTo(ValidImage));
        }

        [Test]
        public async Task VanillaSuccessIsKept()
        {
            var calls = 0;
            var preview = await IconResolver.GetPreviewWithFallback((tag, vanilla, size) =>
            {
                calls++;
                return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = vanilla ? ValidImage : null });
            }, "SORROW_HELMET", true);

            Assert.That(preview.Image, Is.EqualTo(ValidImage));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void NotAllowedIsMissing()
        {
            Assert.That(IconResolver.IsMissing(new PreviewService.Preview() { Image = "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo=" }), Is.True);
            Assert.That(IconResolver.IsMissing(null), Is.True);
        }
    }
}
