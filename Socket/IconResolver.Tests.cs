using System.Collections.Generic;
using System.Net;
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
                "SORROW_HELMET_EMPTY", true);

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
        public async Task MissingAssetIsNotRequestedAgain()
        {
            var calls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                calls++;
                return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = null });
            }
            await IconResolver.GetPreviewWithFallback(load, "SHARD_FLORA_NEGCACHE", false);
            var first = calls;
            var again = await IconResolver.GetPreviewWithFallback(load, "SHARD_FLORA_NEGCACHE", false);

            Assert.That(first, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(IconResolver.IsMissing(again), Is.True);
        }

        [Test]
        public async Task FailedVanillaIsNotRetried()
        {
            var vanillaCalls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                if (vanilla)
                {
                    vanillaCalls++;
                    throw new CoflnetException("unkown_item", "missing");
                }
                return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = ValidImage });
            }
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_NEGCACHE", true);
            var second = await IconResolver.GetPreviewWithFallback(load, "VANILLA_NEGCACHE", true);

            Assert.That(vanillaCalls, Is.EqualTo(1));
            Assert.That(second.Image, Is.EqualTo(ValidImage));
        }

        [Test]
        public void NotAllowedIsMissing()
        {
            Assert.That(IconResolver.IsMissing(new PreviewService.Preview() { Image = "cmVxdWVzdGVkIFVSTCBpcyBub3QgYWxsb3dlZAo=" }), Is.True);
            Assert.That(IconResolver.IsMissing(null), Is.True);
        }

        private static PreviewService.Preview Missing(string tag, PreviewService.MissKind kind) =>
            new PreviewService.Preview() { Id = tag, Image = null, Miss = kind };

        /// <summary>
        /// The cached missing marker must not be the object Resolve fills with the placeholder:
        /// the second response has to look exactly like the first (short cache), not like a found icon.
        /// </summary>
        [Test]
        public async Task SecondMissingResponseHasSameShortCacheAsFirst()
        {
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size) => Task.FromResult(Missing(tag, PreviewService.MissKind.Conclusive));
            var responses = new List<(PreviewService.Preview, TimeSpan)>();
            for (var i = 0; i < 2; i++)
            {
                var loaded = await IconResolver.GetPreviewWithFallback(load, "SHARD_TWICE_NEGCACHE", false);
                Assert.That(IconResolver.IsMissing(loaded), Is.True, "response " + i);
                var placeholder = IconResolver.BuildMissingPreview("SHARD_TWICE_NEGCACHE", loaded);
                responses.Add((placeholder, IconResolver.MissingCacheTime));
                Assert.That(IconResolver.CacheControlFor(responses[i].Item2), Is.EqualTo("public,max-age=120"));
            }
            // the placeholder is a new object, so the cache is never filled with an image
            var third = await IconResolver.GetPreviewWithFallback(load, "SHARD_TWICE_NEGCACHE", false);
            Assert.That(IconResolver.IsMissing(third), Is.True);
            Assert.That(third.Image, Is.Null);
            Assert.That(responses[0].Item1, Is.Not.SameAs(responses[1].Item1));
        }

        [Test]
        public async Task TransientFailureIsRetriedOnNextRequest()
        {
            var calls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                calls++;
                return Task.FromResult(Missing(tag, PreviewService.MissKind.Transient));
            }
            await IconResolver.GetPreviewWithFallback(load, "TRANSIENT_NEGCACHE", false);
            await IconResolver.GetPreviewWithFallback(load, "TRANSIENT_NEGCACHE", false);

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public async Task NotFoundIsNotRetried()
        {
            var calls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                calls++;
                return Task.FromResult(Missing(tag, PreviewService.MissKind.Conclusive));
            }
            await IconResolver.GetPreviewWithFallback(load, "NOTFOUND_NEGCACHE", false);
            await IconResolver.GetPreviewWithFallback(load, "NOTFOUND_NEGCACHE", false);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task VanillaTimeoutIsRetriedButUnknownItemIsNot()
        {
            var timeouts = 0;
            var unknown = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                if (!vanilla)
                    return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = ValidImage });
                if (tag == "VANILLA_TIMEOUT_NEGCACHE")
                {
                    timeouts++;
                    throw new TaskCanceledException("timeout");
                }
                unknown++;
                throw new CoflnetException("unkown_item", "missing");
            }
            for (var i = 0; i < 2; i++)
            {
                await IconResolver.GetPreviewWithFallback(load, "VANILLA_TIMEOUT_NEGCACHE", true);
                await IconResolver.GetPreviewWithFallback(load, "VANILLA_UNKNOWN_NEGCACHE", true);
            }

            Assert.That(timeouts, Is.EqualTo(2));
            Assert.That(unknown, Is.EqualTo(1));
        }

        [Test]
        public async Task GenuineVanillaMissWithoutThrowIsRemembered()
        {
            var vanillaCalls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                if (vanilla)
                {
                    vanillaCalls++;
                    return Task.FromResult(Missing(tag, PreviewService.MissKind.Conclusive));
                }
                return Task.FromResult(new PreviewService.Preview() { Id = tag, Image = ValidImage });
            }
            var first = await IconResolver.GetPreviewWithFallback(load, "VANILLA_EMPTY_NEGCACHE", true);
            var second = await IconResolver.GetPreviewWithFallback(load, "VANILLA_EMPTY_NEGCACHE", true);

            Assert.That(first.Image, Is.EqualTo(ValidImage));
            Assert.That(second.Image, Is.EqualTo(ValidImage));
            Assert.That(vanillaCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task InconclusiveVanillaIsNotMarkedMissingByFallbackMiss()
        {
            var vanillaCalls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                if (vanilla)
                {
                    vanillaCalls++;
                    throw new TaskCanceledException("timeout");
                }
                return Task.FromResult(Missing(tag, PreviewService.MissKind.Conclusive));
            }
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_INCONCLUSIVE_NEGCACHE", true);
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_INCONCLUSIVE_NEGCACHE", true);

            Assert.That(vanillaCalls, Is.EqualTo(2), "vanilla is retried");
        }

        [Test]
        public async Task TransientVanillaMissIsNotMarkedMissingByFallbackMiss()
        {
            var vanillaCalls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                if (vanilla)
                    vanillaCalls++;
                return Task.FromResult(Missing(tag, vanilla ? PreviewService.MissKind.Transient : PreviewService.MissKind.Conclusive));
            }
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_TRANSIENTMISS_NEGCACHE", true);
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_TRANSIENTMISS_NEGCACHE", true);

            Assert.That(vanillaCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task ConclusiveVanillaAndFallbackMissIsRemembered()
        {
            var calls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                calls++;
                return Task.FromResult(Missing(tag, PreviewService.MissKind.Conclusive));
            }
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_BOTHMISSING_NEGCACHE", true);
            await IconResolver.GetPreviewWithFallback(load, "VANILLA_BOTHMISSING_NEGCACHE", true);

            Assert.That(calls, Is.EqualTo(2), "one vanilla and one fallback load, nothing on the second request");
        }

        [Test]
        public async Task ProxyNotFoundIsRemembered()
        {
            var calls = 0;
            Task<PreviewService.Preview> load(string tag, bool vanilla, int size)
            {
                calls++;
                return Task.FromResult(Missing(tag, PreviewService.MissKind.ProxyNotFound));
            }
            await IconResolver.GetPreviewWithFallback(load, "PROXY404_NEGCACHE", false);
            await IconResolver.GetPreviewWithFallback(load, "PROXY404_NEGCACHE", false);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(IconResolver.ProxyNotFoundTtl, Is.LessThan(IconResolver.MissingAssetTtl));
        }

        [TestCase(HttpStatusCode.OK, 500, PreviewService.MissKind.None)]
        [TestCase(HttpStatusCode.OK, 0, PreviewService.MissKind.Transient)]
        [TestCase(HttpStatusCode.OK, 10, PreviewService.MissKind.Transient)]
        [TestCase(HttpStatusCode.OK, null, PreviewService.MissKind.Transient)]
        [TestCase(HttpStatusCode.NotFound, 20, PreviewService.MissKind.ProxyNotFound)]
        [TestCase(HttpStatusCode.NotFound, null, PreviewService.MissKind.ProxyNotFound)]
        [TestCase((HttpStatusCode)0, null, PreviewService.MissKind.Transient)]
        [TestCase(HttpStatusCode.InternalServerError, 100, PreviewService.MissKind.Transient)]
        [TestCase(HttpStatusCode.GatewayTimeout, null, PreviewService.MissKind.Transient)]
        public void ProxyResponsesAreClassified(HttpStatusCode status, int? length, PreviewService.MissKind expected)
        {
            Assert.That(PreviewService.ClassifyProxyResponse(status, length), Is.EqualTo(expected));
        }
    }
}
