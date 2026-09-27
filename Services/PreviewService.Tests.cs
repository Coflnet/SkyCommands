using NUnit.Framework;

namespace Coflnet.Sky.Commands.Services
{
    public class PreviewServiceTests
    {
        private const string HeadRenderBase = "http://crafatar:3000";
        private const string SkycryptBase = "https://static.coflnet.com/sky/skycrypt";

        [TestCase("https://textures.minecraft.net/texture/abc123")]
        [TestCase("http://textures.minecraft.net/texture/abc123")]
        public void ConvertTextureUrlToSkull_BuildsCrafatarRenderUrl(string textureUrl)
        {
            var url = PreviewService.ConvertTextureUrlToSkull(HeadRenderBase, textureUrl);

            Assert.That(url, Is.EqualTo("http://crafatar:3000/renders/head/abc123?overlay"));
        }

        [Test]
        public void ConvertMcHeadsUrlToRenderUrl_BuildsCrafatarRenderUrl()
        {
            var url = PreviewService.ConvertMcHeadsUrlToRenderUrl(HeadRenderBase,
                "https://mc-heads.net/head/e019ece125ec79f95178e1af4da2a8b982de71ed42c31cac4b1d2f665355df5a/64");

            Assert.That(url, Is.EqualTo("http://crafatar:3000/renders/head/e019ece125ec79f95178e1af4da2a8b982de71ed42c31cac4b1d2f665355df5a?overlay"));
        }

        [Test]
        public void BuildMirrorHeadUrl_UsedAsRenderFallback()
        {
            var url = PreviewService.BuildMirrorHeadUrl(SkycryptBase, "abc123");

            Assert.That(url, Is.EqualTo("https://static.coflnet.com/sky/skycrypt/api/head/abc123"));
        }

        [Test]
        public void ExtractHeadHashFromRenderUrl_RoundTripsWithBuildHeadRenderUrl()
        {
            var renderUrl = PreviewService.BuildHeadRenderUrl(HeadRenderBase, "abc123");

            var hash = PreviewService.ExtractHeadHashFromRenderUrl(renderUrl);

            Assert.That(hash, Is.EqualTo("abc123"));
        }

        [Test]
        public void ShouldResolveViaHypixelApi_NullIcon_ResolvesViaApi()
        {
            Assert.That(PreviewService.ShouldResolveViaHypixelApi(null, isPet: false), Is.True);
        }

        [Test]
        public void ShouldResolveViaHypixelApi_OwnIconNonVanilla_ResolvesViaApi()
        {
            // regression: previously the own-icon check only triggered `isVanilla && isOwnIcon`,
            // so a non-vanilla request (isVanilla=false) whose IconUrl was our own circular
            // /static/icon/ url never got resolved via the Hypixel API and instead returned the
            // "image unobtainable (loop)" preview with no image - e.g. skull items whose
            // /api/item/{tag} was never present in the skycrypt mirror (heads are only mirrored by
            // texture hash, not tag).
            var isOwnIcon = PreviewService.ShouldResolveViaHypixelApi(
                "https://sky.coflnet.com/static/icon/SOME_SKULL_TAG", isPet: false);

            Assert.That(isOwnIcon, Is.True);
        }

        [Test]
        public void ShouldResolveViaHypixelApi_ExternalIcon_DoesNotResolveViaApi()
        {
            Assert.That(PreviewService.ShouldResolveViaHypixelApi(
                "https://static.coflnet.com/sky/skycrypt/api/item/OAK_LOG", isPet: false), Is.False);
        }

        [Test]
        public void ShouldResolveViaHypixelApi_Pet_NeverResolvesViaApi()
        {
            Assert.That(PreviewService.ShouldResolveViaHypixelApi(
                "https://sky.coflnet.com/static/icon/PET_SKIN_TAG", isPet: true), Is.False);
        }

        [Test]
        public void IsOwnIconUrl_MatchesOnlyOwnPrefix()
        {
            Assert.That(PreviewService.IsOwnIconUrl("https://sky.coflnet.com/static/icon/TAG"), Is.True);
            Assert.That(PreviewService.IsOwnIconUrl("https://static.coflnet.com/sky/skycrypt/api/item/TAG"), Is.False);
            Assert.That(PreviewService.IsOwnIconUrl(null), Is.False);
        }

        [Test]
        public void IsSkycryptMirrorUrl_MatchesConfiguredBaseAndOldHosts()
        {
            Assert.That(PreviewService.IsSkycryptMirrorUrl(SkycryptBase + "/api/item/OAK_LOG", SkycryptBase), Is.True);
            Assert.That(PreviewService.IsSkycryptMirrorUrl("https://sky.shiiyu.moe/api/item/OAK_LOG", SkycryptBase), Is.True);
            Assert.That(PreviewService.IsSkycryptMirrorUrl("https://skycrypt.coflnet.com/api/item/OAK_LOG", SkycryptBase), Is.True);
            Assert.That(PreviewService.IsSkycryptMirrorUrl("https://sky.coflnet.com/static/icon/OAK_LOG", SkycryptBase), Is.False);
        }

        [Test]
        public void BuildProxyPath_KeepsSourceQuery()
        {
            // unescaped, imgproxy would strip ?overlay and render the head without its hat layer
            var source = new System.Uri("http://crafatar:3000/renders/head/abc123?overlay");
            var path = PreviewService.BuildProxyPath(source, 64);
            Assert.That(path, Is.EqualTo("/a/rs:fill:64:64/plain/http%3A%2F%2Fcrafatar%3A3000%2Frenders%2Fhead%2Fabc123%3Foverlay"));

            // RestSharp must not double-encode the escaped source
            var built = new RestSharp.RestClient("http://imgproxy").BuildUri(new RestSharp.RestRequest(path));
            Assert.That(built.AbsoluteUri, Is.EqualTo("http://imgproxy" + path));
        }
    }
}
