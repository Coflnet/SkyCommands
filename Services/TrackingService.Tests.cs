using System.Linq;
using NUnit.Framework;
using RestSharp;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Regression tests for the Matomo site id used by <see cref="TrackingService"/>. The Matomo
    /// instance was reset and the previously hardcoded site id now belongs to a different site, so
    /// hits built with the old hardcoded id would silently be dropped. These tests build the actual
    /// requests (no network calls) and assert they carry the configured idsite/rec params.
    /// </summary>
    public class TrackingServiceTests
    {
        private const string TestBaseUrl = "http://tracking-test.invalid";

        private static string ParamValue(RestRequest request, string name)
        {
            return request.Parameters.FirstOrDefault(p => p.Name == name)?.Value?.ToString();
        }

        [Test]
        public void DefaultSiteId_IsThree()
        {
            Assert.That(TrackingService.DefaultSiteId, Is.EqualTo(3));
        }

        [Test]
        public void TrackPageRequest_UsesDefaultSiteId()
        {
            var service = new TrackingService(TestBaseUrl, TrackingService.DefaultSiteId);

            var request = service.BuildTrackPageRequest("http://sky.coflnet.com/x", "title", null, "ua", default);

            Assert.That(ParamValue(request, "idsite"), Is.EqualTo("3"));
            Assert.That(ParamValue(request, "rec"), Is.EqualTo("1"));
        }

        [Test]
        public void TrackSearchResultRequest_UsesDefaultSiteId()
        {
            var service = new TrackingService(TestBaseUrl, TrackingService.DefaultSiteId);

            var request = service.BuildTrackSearchResultRequest("sword", 5, "ua", "12");

            Assert.That(ParamValue(request, "idsite"), Is.EqualTo("3"));
            Assert.That(ParamValue(request, "rec"), Is.EqualTo("1"));
        }

        [Test]
        public void TrackSearchActionRequest_UsesDefaultSiteId()
        {
            var service = new TrackingService(TestBaseUrl, TrackingService.DefaultSiteId);

            var request = service.BuildTrackSearchActionRequest("sword", "12");

            Assert.That(ParamValue(request, "idsite"), Is.EqualTo("3"));
            Assert.That(ParamValue(request, "rec"), Is.EqualTo("1"));
        }

        [Test]
        public void CommandErrorRequest_UsesDefaultSiteId()
        {
            var service = new TrackingService(TestBaseUrl, TrackingService.DefaultSiteId);

            var request = service.BuildCommandErrorRequest("timeout");

            Assert.That(ParamValue(request, "idsite"), Is.EqualTo("3"));
            Assert.That(ParamValue(request, "rec"), Is.EqualTo("1"));
        }

        [TestCase(3)]
        [TestCase(42)]
        public void AllRequestBuilders_UseConfiguredSiteIdOverride(int siteId)
        {
            var service = new TrackingService(TestBaseUrl, siteId);
            var expected = siteId.ToString();

            Assert.That(ParamValue(service.BuildTrackPageRequest("http://sky.coflnet.com/x", "t", null, "ua", default), "idsite"), Is.EqualTo(expected));
            Assert.That(ParamValue(service.BuildTrackSearchResultRequest("v", 1, "ua", "1"), "idsite"), Is.EqualTo(expected));
            Assert.That(ParamValue(service.BuildTrackSearchActionRequest("v", "1"), "idsite"), Is.EqualTo(expected));
            Assert.That(ParamValue(service.BuildCommandErrorRequest("err"), "idsite"), Is.EqualTo(expected));
        }

        [Test]
        public void ParameterlessConstructor_ReadsSiteIdFromConfig()
        {
            // exercises the real production wiring (SimplerConfig -> appsettings.json MATOMO:SITE_ID)
            // used by the static TrackingService.Instance singleton, without any network call.
            var service = new TrackingService();

            var request = service.BuildCommandErrorRequest("config_smoke_test");

            Assert.That(ParamValue(request, "idsite"), Is.EqualTo("3"));
            Assert.That(ParamValue(request, "rec"), Is.EqualTo("1"));
        }
    }
}
