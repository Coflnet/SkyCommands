using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    /// <summary>
    /// Shares premium tier lookups between all sockets of a user so the periodic tier refresh
    /// of every connection doesn't hit the premium api. Lookups of one user are single-flight.
    /// With the 60s ttl the premium api sees at most one lookup per distinct logged-in user per minute
    /// (plus the uncached subscribe lookups, which also feed this cache).
    /// </summary>
    internal class UserTierCache
    {
        /// <summary>How long a successfully looked up tier is reused (or until it expires, if sooner)</summary>
        public static readonly TimeSpan TierTtl = TimeSpan.FromSeconds(60);
        /// <summary>How long a failed lookup is remembered, so a failing api is queried at most once per window</summary>
        public static readonly TimeSpan FailureTtl = TimeSpan.FromSeconds(30);
        /// <summary>A lookup taking longer than this counts as failed so nobody is stuck behind a hung request</summary>
        public static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(10);
        /// <summary>How long after its expiry the last successfully looked up tier is still trusted when lookups fail</summary>
        public static readonly TimeSpan KnownTierGrace = TimeSpan.FromMinutes(10);
        public const int DefaultMaxEntries = 10_000;

        public static readonly (AccountTier?, DateTime) Failure = (null, default);

        private class Entry
        {
            public TaskCompletionSource<(AccountTier?, DateTime)> Source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime StartedAt;
            /// <summary>set once the lookup finished without a tier</summary>
            public bool Failed;
            /// <summary>null while the lookup is in flight</summary>
            public DateTime? ValidUntil;
        }

        private readonly Dictionary<int, Entry> entries = new();
        /// <summary>tier, expiry and start time of the newest successful lookup per user, independent of what the entries hold</summary>
        private readonly Dictionary<int, (AccountTier Tier, DateTime Expiry, DateTime StartedAt)> lastSuccess = new();
        private readonly object gate = new();
        private readonly Func<DateTime> clock;
        private readonly TimeSpan timeout;
        private readonly int maxEntries;

        public UserTierCache(Func<DateTime> clock = null, TimeSpan? lookupTimeout = null, int maxEntries = DefaultMaxEntries)
        {
            this.clock = clock ?? (() => DateTime.UtcNow);
            timeout = lookupTimeout ?? LookupTimeout;
            this.maxEntries = maxEntries;
        }

        public int Count { get { lock (gate) return entries.Count; } }

        public DateTime Now => clock();

        /// <summary>
        /// The tier of the last successful lookup of the user, if it is recent enough to keep trusting while lookups fail
        /// </summary>
        public bool TryGetKnownTier(int userId, out AccountTier tier)
        {
            lock (gate)
            {
                var found = lastSuccess.TryGetValue(userId, out var known) && clock() <= WithGrace(known.Expiry);
                tier = found ? known.Tier : AccountTier.NONE;
                return found;
            }
        }

        /// <summary>expiry plus grace, clamped because the premium api can return values at DateTime.MaxValue</summary>
        private static DateTime WithGrace(DateTime expiry)
            => expiry > DateTime.MaxValue - KnownTierGrace ? DateTime.MaxValue : expiry + KnownTierGrace;

        /// <summary>
        /// Returns the (shared) tier of the user, a null tier means the lookup failed
        /// </summary>
        public async Task<(AccountTier?, DateTime)> Get(int userId, Func<string, Task<(AccountTier?, DateTime)>> lookup)
        {
            Entry entry;
            var shared = false;
            lock (gate)
            {
                var now = clock();
                shared = entries.TryGetValue(userId, out entry)
                    && (entry.ValidUntil == null ? now - entry.StartedAt <= timeout : entry.ValidUntil > now);
                if (!shared)
                    entry = StartEntry(userId, now);
            }
            if (shared)
                return await entry.Source.Task.ConfigureAwait(false);
            return await RunLookup(userId, entry, lookup, timeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Always performs the remote lookup (for the subscribe command) and stores a successful result,
        /// so the cache is never older than the newest direct lookup of the user.
        /// </summary>
        public async Task<(AccountTier?, DateTime)> Fetch(int userId, Func<string, Task<(AccountTier?, DateTime)>> lookup)
        {
            Entry entry;
            lock (gate)
                entry = new Entry() { StartedAt = clock() };
            // a failure says nothing, so it never replaces a good cached value
            // no timeout here: a slow premium api must not put a paying user into the slow set (the old subscribe waited for it)
            return await RunLookup(userId, entry, lookup, Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        }

        private Entry StartEntry(int userId, DateTime now)
        {
            if (entries.Count >= maxEntries)
                Prune(now);
            var entry = new Entry() { StartedAt = now };
            entries[userId] = entry;
            return entry;
        }

        private async Task<(AccountTier?, DateTime)> RunLookup(int userId, Entry entry, Func<string, Task<(AccountTier?, DateTime)>> lookup, TimeSpan lookupTimeout)
        {
            var result = Failure;
            try
            {
                var task = Task.Run(() => lookup(userId.ToString()));
                using var cancel = new CancellationTokenSource();
                var finished = await Task.WhenAny(task, Task.Delay(lookupTimeout, cancel.Token)).ConfigureAwait(false);
                if (finished == task)
                {
                    cancel.Cancel();
                    result = await task.ConfigureAwait(false);
                }
                else
                {
                    // the abandoned lookup may still fail later, observe it
                    _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    LogFailure(userId, "timed out");
                }
            }
            catch (Exception e)
            {
                LogFailure(userId, e.GetType().Name + ": " + e.Message);
                result = Failure;
            }
            finally
            {
                // must never leave waiters hanging
                Complete(userId, entry, result);
            }
            return result;
        }

        private void Complete(int userId, Entry entry, (AccountTier?, DateTime) result)
        {
            try
            {
                lock (gate)
                {
                    var now = clock();
                    var ttl = result.Item1 == null ? FailureTtl : TierTtl;
                    if (result.Item1 != null && result.Item2 - now < ttl)
                        ttl = result.Item2 - now;
                    entry.ValidUntil = now + (ttl < TimeSpan.Zero ? TimeSpan.Zero : ttl);
                    entry.Failed = result.Item1 == null;
                    if (result.Item1 != null)
                    {
                        // the start time only decides between two successes, a failure never shadows one
                        if (!lastSuccess.TryGetValue(userId, out var known) || known.StartedAt <= entry.StartedAt)
                            lastSuccess[userId] = (result.Item1.Value, result.Item2, entry.StartedAt);
                        if (!entries.TryGetValue(userId, out var cached))
                            cached = null;
                        if (!ReferenceEquals(cached, entry) && (cached == null || cached.Failed || cached.StartedAt <= entry.StartedAt))
                        {
                            if (cached == null && entries.Count >= maxEntries)
                                Prune(now);
                            entries[userId] = entry;
                        }
                    }
                }
            }
            finally
            {
                entry.Source.TrySetResult(result);
            }
        }

        private static void LogFailure(int userId, string reason)
        {
            try
            {
                dev.Logger.Factory.CreateLogger("UserTierCache").LogWarning("account tier lookup failed for user {UserId}: {Reason}", userId, reason);
            }
            catch (Exception)
            {
                // logging must never prevent completing the shared lookup
            }
        }

        private void Prune(DateTime now)
        {
            foreach (var key in entries.Where(e => e.Value.ValidUntil == null ? now - e.Value.StartedAt > timeout : e.Value.ValidUntil <= now).Select(e => e.Key).ToList())
                entries.Remove(key);
            foreach (var key in lastSuccess.Where(e => WithGrace(e.Value.Expiry) < now).Select(e => e.Key).ToList())
                lastSuccess.Remove(key);
            if (entries.Count < maxEntries)
                return;
            // still full of live entries, drop the oldest finished ones down to ~90% (in-flight lookups stay shared)
            var target = (int)(maxEntries * 0.9);
            foreach (var key in entries.Where(e => e.Value.ValidUntil != null).OrderBy(e => e.Value.ValidUntil).Select(e => e.Key).Take(entries.Count - target).ToList())
                entries.Remove(key);
            if (lastSuccess.Count > maxEntries)
                foreach (var key in lastSuccess.OrderBy(e => e.Value.Expiry).Select(e => e.Key).Take(lastSuccess.Count - target).ToList())
                    lastSuccess.Remove(key);
        }
    }
}
