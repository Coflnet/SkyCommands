using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;
using NUnit.Framework;

namespace Coflnet.Sky.Commands
{
    public class SkyblockBackEndTests
    {
        private static long idSeed = 500_000_000;

        [SetUp]
        public void Setup()
        {
            // OnError/OnClose both funnel through FlipperService.RemoveConnection; give them a real
            // (but otherwise inert) instance instead of whatever AddCoflService wired up.
            DiHandler.OverrideService<FlipperService, FlipperService>(new FlipperService(null, null));
        }

        /// <summary>
        /// Regression test for the connection-leak fix: OnError used to tear down app state via the
        /// old app-level Close() but never closed the library session, while OnClose (fired directly,
        /// or re-entrantly once OnError now force-closes the session) runs the same teardown again.
        /// CleanupConnection must tolerate being called twice without throwing and without leaving
        /// the connection registered.
        /// </summary>
        [Test]
        public void CleanupConnectionIsIdempotent()
        {
            var id = Interlocked.Increment(ref idSeed);
            var connection = new SkyblockBackEnd { Id = id };
            SkyblockBackEnd.Subscribers.TryAdd(id, connection);

            Assert.DoesNotThrow(() =>
            {
                connection.CleanupConnection(); // simulates OnError's call
                connection.CleanupConnection(); // simulates the (re-entrant) OnClose call afterwards
            });

            Assert.That(SkyblockBackEnd.Subscribers.ContainsKey(id), Is.False);
        }

        [Test]
        public void CleanupConnectionOnUnregisteredConnectionDoesNotThrow()
        {
            var connection = new SkyblockBackEnd();

            Assert.DoesNotThrow(() => connection.CleanupConnection());
        }

        private class Lookup
        {
            public int Calls;
            public (AccountTier?, DateTime) Result = (AccountTier.PREMIUM, DateTime.UtcNow.AddDays(1));
            public Task Gate;
            public Func<Task> Hook;
            public async Task<(AccountTier?, DateTime)> Run(string id)
            {
                Interlocked.Increment(ref Calls);
                if (Hook != null)
                    await Hook();
                if (Gate != null)
                    await Gate;
                return Result;
            }
        }

        private static SkyblockBackEnd Subscribed(int userId, AccountTier session = AccountTier.NONE)
            => new SkyblockBackEnd { UserId = userId, SessionTier = session, HasFlipSubscription = true };

        [Test]
        public async Task AnonymousConnectionHasNoTierAndSkipsLookup()
        {
            var connection = new SkyblockBackEnd { SessionTier = AccountTier.PREMIUM, HasFlipSubscription = true };
            var lookup = new Lookup();

            var tier = await connection.ResolveAccountTier(lookup.Run, new UserTierCache());

            Assert.That(tier, Is.EqualTo(AccountTier.NONE));
            Assert.That(lookup.Calls, Is.EqualTo(0));
        }

        [TestCase(AccountTier.PREMIUM_PLUS)]
        [TestCase(AccountTier.PREMIUM)]
        [TestCase(AccountTier.STARTER_PREMIUM)]
        [TestCase(AccountTier.NONE)]
        public async Task LoggedInConnectionReturnsCurrentTier(AccountTier current)
        {
            var connection = Subscribed(7, AccountTier.PREMIUM);
            var lookup = new Lookup { Result = (current, DateTime.UtcNow.AddDays(1)) };

            var tier = await connection.ResolveAccountTier(lookup.Run, new UserTierCache());

            Assert.That(tier, Is.EqualTo(current));
            Assert.That(connection.SessionTier, Is.EqualTo(current));
        }

        /// <summary>The subscribe command puts SUPER_PREMIUM in the slow set, the refresh must not promote it</summary>
        [Test]
        public async Task SuperPremiumYieldsTheSetTheSubscribePathGives()
        {
            var connection = Subscribed(7);
            var lookup = new Lookup { Result = (AccountTier.SUPER_PREMIUM, DateTime.UtcNow.AddDays(1)) };

            var tier = await connection.ResolveAccountTier(lookup.Run, new UserTierCache());

            Assert.That(tier, Is.EqualTo(AccountTier.NONE));
            Assert.That(connection.SessionTier, Is.EqualTo(AccountTier.SUPER_PREMIUM), "same value the subscribe command stores");
        }

        [Test]
        public async Task ConnectionWithoutSuccessfulSubscriptionIsNotPromoted()
        {
            var connection = new SkyblockBackEnd { UserId = 7, HasFlipSubscription = false };
            var lookup = new Lookup { Result = (AccountTier.PREMIUM_PLUS, DateTime.UtcNow.AddDays(1)) };

            var tier = await connection.ResolveAccountTier(lookup.Run, new UserTierCache());

            Assert.That(tier, Is.EqualTo(AccountTier.NONE));
            Assert.That(lookup.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task SecondCallWithinTtlUsesCacheAndSharesBetweenSockets()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddDays(1)) };

            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);
            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);
            Assert.That(lookup.Calls, Is.EqualTo(1));

            now += UserTierCache.TierTtl + TimeSpan.FromSeconds(1);
            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);
            Assert.That(lookup.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task CacheEndsAtTierExpiryWhenSooner()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddSeconds(40)) };

            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);
            now += TimeSpan.FromSeconds(41);
            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);

            Assert.That(lookup.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task ConcurrentCallsOfOneUserLookUpOnce()
        {
            var release = new TaskCompletionSource();
            var lookup = new Lookup { Gate = release.Task };
            var cache = new UserTierCache();

            var tasks = Enumerable.Range(0, 5).Select(_ => Subscribed(7).ResolveAccountTier(lookup.Run, cache)).ToList();
            // every call registered with the cache synchronously before its first await
            release.SetResult();
            var tiers = await Task.WhenAll(tasks);

            Assert.That(lookup.Calls, Is.EqualTo(1));
            Assert.That(tiers, Is.All.EqualTo(AccountTier.PREMIUM));
        }

        [Test]
        public async Task FailedLookupKeepsKnownTierAndRetriesAfterShortWindow()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddHours(1)) };
            var connection = Subscribed(7);
            Assert.That(await connection.ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.PREMIUM));

            now += UserTierCache.TierTtl + TimeSpan.FromSeconds(1);
            lookup.Result = (null, now);
            var failed = await connection.ResolveAccountTier(lookup.Run, cache);
            var stillFailed = await connection.ResolveAccountTier(lookup.Run, cache);
            Assert.That(failed, Is.EqualTo(AccountTier.PREMIUM));
            Assert.That(stillFailed, Is.EqualTo(AccountTier.PREMIUM));
            Assert.That(lookup.Calls, Is.EqualTo(2), "failure is cached for the short window only");

            now += UserTierCache.FailureTtl + TimeSpan.FromSeconds(1);
            lookup.Result = (AccountTier.NONE, now.AddHours(3));
            var recovered = await connection.ResolveAccountTier(lookup.Run, cache);

            Assert.That(lookup.Calls, Is.EqualTo(3));
            Assert.That(recovered, Is.EqualTo(AccountTier.NONE));
        }

        [Test]
        public async Task KnownTierIsOnlyKeptUntilExpiryPlusGrace()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var expiry = now.AddMinutes(5);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, expiry) };
            var connection = Subscribed(7);
            await connection.ResolveAccountTier(lookup.Run, cache);
            lookup.Result = (null, now);

            now = expiry + UserTierCache.KnownTierGrace - TimeSpan.FromSeconds(1);
            Assert.That(await connection.ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.PREMIUM), "inside the grace period");

            now = expiry + UserTierCache.KnownTierGrace + TimeSpan.FromMinutes(1);
            Assert.That(await connection.ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.NONE), "after the grace period");
        }

        [Test]
        public async Task FailureWithoutAnySuccessfulLookupIsNone()
        {
            var connection = Subscribed(7, AccountTier.PREMIUM);
            var lookup = new Lookup { Result = (null, DateTime.UtcNow) };

            Assert.That(await connection.ResolveAccountTier(lookup.Run, new UserTierCache()), Is.EqualTo(AccountTier.NONE));
        }

        /// <summary>A user who just bought premium and re-subscribed must not be downgraded by an older cached NONE</summary>
        [Test]
        public async Task SubscribeLookupRefreshesCachedTier()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var lookup = new Lookup { Result = (AccountTier.NONE, now.AddHours(3)) };
            var connection = Subscribed(7);
            Assert.That(await connection.ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.NONE));

            lookup.Result = (AccountTier.PREMIUM, now.AddDays(30));
            var subscribed = await cache.Fetch(7, lookup.Run);
            Assert.That(subscribed.Item1, Is.EqualTo(AccountTier.PREMIUM));
            var calls = lookup.Calls;

            Assert.That(await connection.ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.PREMIUM));
            Assert.That(lookup.Calls, Is.EqualTo(calls), "served from the cache the subscribe lookup filled");
        }

        [Test]
        public async Task FailedSubscribeLookupDoesNotShadowCachedTier()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddDays(1)) };
            await Subscribed(7).ResolveAccountTier(lookup.Run, cache);

            lookup.Result = (null, now);
            await cache.Fetch(7, lookup.Run);
            lookup.Result = (AccountTier.PREMIUM, now.AddDays(1));
            var calls = lookup.Calls;

            Assert.That(await Subscribed(7).ResolveAccountTier(lookup.Run, cache), Is.EqualTo(AccountTier.PREMIUM));
            Assert.That(lookup.Calls, Is.EqualTo(calls));
        }

        [Test]
        public async Task HungLookupFailsAfterTimeoutAndNextCallStartsNewLookup()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now, TimeSpan.FromMilliseconds(50));
            var hung = new TaskCompletionSource<(AccountTier?, DateTime)>();

            var first = await cache.Get(7, _ => hung.Task);
            Assert.That(first.Item1, Is.Null);

            now += UserTierCache.FailureTtl + TimeSpan.FromSeconds(1);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddDays(1)) };
            var second = await cache.Get(7, lookup.Run);

            Assert.That(second.Item1, Is.EqualTo(AccountTier.PREMIUM));
            Assert.That(lookup.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task WaitersBehindHungLookupAreReleasedAndStaleInFlightIsReplaceable()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now, TimeSpan.FromMilliseconds(100));
            var hung = new TaskCompletionSource<(AccountTier?, DateTime)>();
            var owner = cache.Get(7, _ => hung.Task);
            var waiter = cache.Get(7, _ => throw new InvalidOperationException("must share"));

            Assert.That((await waiter).Item1, Is.Null);
            Assert.That((await owner).Item1, Is.Null);
        }

        [Test]
        public async Task ThrowingLookupReleasesAllConcurrentWaiters()
        {
            var release = new TaskCompletionSource();
            var cache = new UserTierCache();
            async Task<(AccountTier?, DateTime)> lookup(string id)
            {
                await release.Task;
                throw new InvalidOperationException("api down");
            }

            var tasks = Enumerable.Range(0, 4).Select(_ => cache.Get(7, lookup)).ToList();
            release.SetResult();
            var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(results.Select(r => r.Item1), Is.All.Null);
        }

        [Test]
        public async Task FullCachePrunesExpiredThenOnlyTheOldestEntries()
        {
            var now = DateTime.UtcNow;
            var cache = new UserTierCache(() => now, maxEntries: 10);
            var lookup = new Lookup { Result = (AccountTier.PREMIUM, now.AddDays(1)) };
            for (var user = 1; user <= 10; user++)
            {
                await cache.Get(user, lookup.Run);
                now += TimeSpan.FromMilliseconds(10);
            }
            Assert.That(cache.Count, Is.EqualTo(10));
            var calls = lookup.Calls;

            await cache.Get(11, lookup.Run);

            Assert.That(cache.Count, Is.InRange(9, 10), "not everything dropped at once");
            await cache.Get(10, lookup.Run);
            Assert.That(lookup.Calls, Is.EqualTo(calls + 1), "newest entries survive");
            await cache.Get(1, lookup.Run);
            Assert.That(lookup.Calls, Is.EqualTo(calls + 2), "oldest entry was dropped");
        }

        [Test]
        public async Task ThrowingLookupKeepsKnownTier()
        {
            var connection = Subscribed(7, AccountTier.PREMIUM);

            var tier = await connection.ResolveAccountTier(_ => throw new System.Exception("down"), new UserTierCache());

            Assert.That(tier, Is.EqualTo(AccountTier.NONE), "no successful lookup to base the known tier on");
        }

        [Test]
        public async Task UserChangeDuringLookupDoesNotAssignStaleTier()
        {
            var connection = Subscribed(7, AccountTier.NONE);
            var cache = new UserTierCache();
            var seen = new List<string>();
            Task<(AccountTier?, DateTime)> lookup(string id)
            {
                seen.Add(id);
                if (id == "7")
                {
                    connection.UserId = 8; // account switch while the first lookup runs
                    return Task.FromResult<(AccountTier?, DateTime)>((AccountTier.PREMIUM_PLUS, DateTime.UtcNow.AddDays(1)));
                }
                return Task.FromResult<(AccountTier?, DateTime)>((AccountTier.STARTER_PREMIUM, DateTime.UtcNow.AddDays(1)));
            }

            var tier = await connection.ResolveAccountTier(lookup, cache);

            Assert.That(seen, Is.EqualTo(new[] { "7", "8" }));
            Assert.That(tier, Is.EqualTo(AccountTier.STARTER_PREMIUM));
            Assert.That(connection.SessionTier, Is.EqualTo(AccountTier.STARTER_PREMIUM));
        }
    }
}
