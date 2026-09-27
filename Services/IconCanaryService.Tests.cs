using System.Collections.Generic;
using NUnit.Framework;

namespace Coflnet.Sky.Commands.Services
{
    public class IconCanaryServiceTests
    {
        private static PreviewService.Preview Ok() => new PreviewService.Preview()
        {
            Id = "TEST",
            Image = "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAQAAAAAYLlVAAAAOUlEQVR42u3OIQEAAAACIP1/2hkWWEBzVgEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAYF3YDicAEE8VTiYAAAAAElFTkSuQmCC",
            MimeType = "image/png"
        };

        [Test]
        public void IsHealthy_NullPreview_IsUnhealthy()
        {
            Assert.That(IconCanaryService.IsHealthy(null), Is.False);
        }

        [Test]
        public void IsHealthy_MissingImage_IsUnhealthy()
        {
            Assert.That(IconCanaryService.IsHealthy(new PreviewService.Preview() { Id = "TEST", Image = null }), Is.False);
        }

        [Test]
        public void IsHealthy_NotAllowedPlaceholder_IsUnhealthy()
        {
            // regression: imgproxy returning the "requested URL is not allowed" body (e.g. an
            // IMGPROXY_ALLOWED_SOURCES entry that stopped matching, or a source-address SSRF
            // guard flipping to a stricter default) must be treated as a failed canary check,
            // not a successful one - the base64 blob is short but not null, so IsMissing has to
            // special-case it (see IconResolver.IsMissing).
            Assert.That(IconCanaryService.IsHealthy(new PreviewService.Preview()
            {
                Image = "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo="
            }), Is.False);
        }

        [Test]
        public void IsHealthy_RealImage_IsHealthy()
        {
            Assert.That(IconCanaryService.IsHealthy(Ok()), Is.True);
        }

        [Test]
        public void Evaluate_MapsEachSourceIndependently()
        {
            var previews = new Dictionary<string, PreviewService.Preview>
            {
                ["mirror"] = Ok(),
                ["crafatar"] = null,
                ["vanilla"] = Ok(),
            };

            var result = IconCanaryService.Evaluate(previews);

            Assert.That(result["mirror"], Is.True);
            Assert.That(result["crafatar"], Is.False);
            Assert.That(result["vanilla"], Is.True);
        }

        [Test]
        public void Evaluate_AllSourcesDown_AllUnhealthy()
        {
            // this is the "systemic imgproxy failure" case (e.g. imgproxy pod crash-looping, or a
            // config regression that breaks every source at once) - every source must independently
            // report unhealthy so the sky_icon_canary_ok{source=...} gauge alerts on all of them.
            var previews = new Dictionary<string, PreviewService.Preview>
            {
                ["mirror"] = null,
                ["crafatar"] = null,
                ["vanilla"] = null,
            };

            var result = IconCanaryService.Evaluate(previews);

            Assert.That(result.Values, Has.All.False);
        }
    }
}
