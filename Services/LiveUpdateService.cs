using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;
using Coflnet.Sky.Filter;
using Coflnet.Sky.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Coflnet.Sky.Commands;

/// <summary>
/// Pushes live updates to websocket connections. Every connection has at most one active
/// subscription; subscribing again atomically replaces the previous one, so the frontend only
/// ever declares "the one topic I care about right now" and never has to explicitly unsubscribe.
/// Two topic kinds are supported:
///  - <c>auction/{uuid}</c>: whenever a new bid is placed on that auction the full auction
///    state is resent (message type <c>auctionUpdate</c>) so the frontend can rerender.
///  - <c>sold/{tag}</c>: whenever an auction of that item tag is sold (and matches the optional
///    filter the user selected) a recent-auction preview is pushed (message type <c>soldAuction</c>).
///  - <c>active/{tag[,tag...]}</c> (or <c>active/*</c> with a mandatory filter): new matching auctions are pushed
///    (message type <c>activeAuction</c>, also on new bids of already sent auctions) and sent auctions that were
///    sold/ended/cancelled are removed again (message type <c>auctionGone</c>).
/// </summary>
public class LiveUpdateService : BackgroundService
{
    private readonly IConfiguration config;
    private readonly FilterEngine filterEngine;
    private readonly ILogger<LiveUpdateService> logger;

    /// <summary>The single active subscription per connection (also carries the generation <c>mId</c>).</summary>
    private readonly ConcurrentDictionary<ILiveUpdateConnection, TopicSub> byConnection = new();
    /// <summary>Bid-update subscriptions keyed by auction uuid (for O(1) routing).</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ILiveUpdateConnection, TopicSub>> auctionSubs = new();
    /// <summary>Sold-auction subscriptions keyed by item tag (for O(1) routing).</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<ILiveUpdateConnection, TopicSub>> soldSubs = new();
    /// <summary>Active-auction subscriptions keyed by item tag.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<TopicSub, byte>> activeTagSubs = new();
    /// <summary>Active-auction subscriptions for all tags (<c>active/*</c>, always carry a filter).</summary>
    private readonly ConcurrentDictionary<TopicSub, byte> activeWildcardSubs = new();
    /// <summary>Reverse index: auction uuid to the active subscriptions that already received it.</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<TopicSub, byte>> sentIndex = new();
    /// <summary>Connections that already have the close handler registered.</summary>
    private readonly ConcurrentDictionary<ILiveUpdateConnection, byte> closeRegistered = new();
    /// <summary>Resolves player names, replaceable for tests.</summary>
    internal Func<string, Task<string>> NameResolver = id => PlayerSearch.Instance.GetNameWithCacheAsync(id);
    /// <summary>Serializes the multi-step (un)subscribe so a connection can never end up in two indexes.</summary>
    private readonly object subLock = new();

    private static readonly Prometheus.Counter subscribeCount = Prometheus.Metrics
        .CreateCounter("sky_commands_liveupdate_subscribe", "How many live update subscriptions were created");
    private static readonly Prometheus.Counter pushCount = Prometheus.Metrics
        .CreateCounter("sky_commands_liveupdate_push", "How many live updates were pushed", "kind");
    private static readonly Prometheus.Gauge activeSubsGauge = Prometheus.Metrics
        .CreateGauge("sky_commands_liveupdate_active", "How many connections have an active live update subscription");
    private static readonly Prometheus.Gauge activeKindGauge = Prometheus.Metrics
        .CreateGauge("sky_commands_liveupdate_active_kind", "How many connections have an active-auction-explorer subscription");

    public LiveUpdateService(IConfiguration config, FilterEngine filterEngine, ILogger<LiveUpdateService> logger)
    {
        this.config = config;
        this.filterEngine = filterEngine;
        this.logger = logger;
    }

    /// <summary>
    /// (Re)subscribes a connection to a single topic, atomically replacing any previous subscription.
    /// </summary>
    /// <param name="con">the websocket connection</param>
    /// <param name="mId">message id of the subscribe request; also used as a generation number so a
    /// reordered (older) subscribe can never override a newer one, and push messages reuse it</param>
    /// <param name="topic">topic string, e.g. <c>auction/{uuid}</c>, <c>sold/{tag}</c> or <c>active/{tag,tag}</c></param>
    /// <param name="filter">optional filter (used for <c>sold</c> and <c>active</c> topics)</param>
    /// <param name="knownUuids">auction uuids the client already shows (<c>active</c> only, at most 200 are used)</param>
    public void Subscribe(ILiveUpdateConnection con, long mId, string topic, Dictionary<string, string> filter, IEnumerable<string> knownUuids = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new CoflnetException("invalid_topic", "A `topic` is required to subscribe");
        var slashIndex = topic.IndexOf('/');
        if (slashIndex <= 0 || slashIndex >= topic.Length - 1)
            throw new CoflnetException("invalid_topic", "The `topic` has to be of the form `auction/{uuid}`, `sold/{tag}` or `active/{tags}`");
        var kind = topic[..slashIndex];
        var value = topic[(slashIndex + 1)..];

        SubKind subKind;
        Func<SaveAuction, bool> matcher = null;
        string[] tags = null;
        var clean = CleanFilter(filter);
        switch (kind)
        {
            case "auction":
                subKind = SubKind.Auction;
                break;
            case "sold":
                subKind = SubKind.Sold;
                if (clean.Count > 0)
                    matcher = CompileMatcher(clean);
                break;
            case "active":
                subKind = SubKind.Active;
                tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(t => t.ToUpperInvariant()).Distinct().ToArray();
                if (tags.Length == 0)
                    throw new CoflnetException("invalid_topic", "At least one item tag is required for `active/{tags}`");
                // page-state keys of the explorer are no FilterEngine filters; the name search is a separate predicate
                var nameContains = ExtractActiveFilter(clean);
                var hasFilter = clean.Count > 0 || nameContains != null;
                if (tags.Contains("*"))
                {
                    if (tags.Length > 1)
                        throw new CoflnetException("invalid_topic", "`*` can't be combined with other tags");
                    if (!hasFilter)
                        throw new CoflnetException("filter_required", "Subscribing to all items (`active/*`) requires at least one filter");
                }
                else if (tags.Length > MaxActiveTags)
                    throw new CoflnetException("too_many_tags", $"At most {MaxActiveTags} item tags can be subscribed at once");
                if (clean.Count > 0)
                    matcher = CompileMatcher(clean);
                if (nameContains != null)
                {
                    var inner = matcher;
                    matcher = a => (inner == null || inner(a))
                        && a.ItemName?.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                break;
            default:
                throw new CoflnetException("invalid_topic", $"Unknown topic kind `{kind}`. Supported: `auction`, `sold`, `active`");
        }

        var sub = new TopicSub(con, mId, subKind == SubKind.Active ? string.Join(",", tags) : value, subKind, matcher, tags);
        if (subKind == SubKind.Active && knownUuids != null)
        {
            foreach (var uuid in knownUuids.Where(u => !string.IsNullOrEmpty(u)).Distinct().Take(MaxKnownUuids))
                sub.TryRecord(uuid, out _);
        }
        lock (subLock)
        {
            // messages of one connection are dispatched on independent tasks, so a newer subscribe may
            // have already been applied - never let an older one clobber it (keeps the one-topic invariant)
            if (byConnection.TryGetValue(con, out var current) && current.MId > mId)
                return;
            RemoveFromIndex(con, current);
            byConnection[con] = sub;
            switch (subKind)
            {
                case SubKind.Auction:
                    auctionSubs.GetOrAdd(value, _ => new())[con] = sub;
                    break;
                case SubKind.Sold:
                    soldSubs.GetOrAdd(value, _ => new())[con] = sub;
                    break;
                default:
                    if (sub.IsWildcard)
                        activeWildcardSubs[sub] = 0;
                    else
                        foreach (var tag in sub.Tags)
                            activeTagSubs.GetOrAdd(tag, _ => new())[sub] = 0;
                    foreach (var uuid in sub.SnapshotSent())
                        sentIndex.GetOrAdd(uuid, _ => new())[sub] = 0;
                    break;
            }
            // clean up when the connection closes (single registration per connection)
            if (closeRegistered.TryAdd(con, 0))
                con.RegisterCloseHandler(() =>
                {
                    closeRegistered.TryRemove(con, out _);
                    Unsubscribe(con);
                });
            activeSubsGauge.Set(byConnection.Count);
            activeKindGauge.Set(byConnection.Values.Count(v => v.Kind == SubKind.Active));
        }
        subscribeCount.Inc();
    }

    private static readonly string[] ActivePageKeys = { "armorSet", "categories", "tags", "q", "orderBy", "perTagLimit", "page", "pageSize" };

    /// <summary>Removes explorer page-state keys from the dict (in place) and returns the item name search, if any.</summary>
    private static string ExtractActiveFilter(Dictionary<string, string> clean)
    {
        foreach (var key in clean.Keys.Where(k => ActivePageKeys.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
            clean.Remove(key);
        string contains = null;
        foreach (var key in clean.Keys.Where(k => string.Equals(k, "itemNameContains", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (!string.IsNullOrWhiteSpace(clean[key]))
                contains = clean[key].Trim();
            clean.Remove(key);
        }
        return contains;
    }

    private Func<SaveAuction, bool> CompileMatcher(Dictionary<string, string> clean)
    {
        try
        {
            return filterEngine.GetMatcher(clean);
        }
        catch (Exception e)
        {
            logger.LogInformation(e, "invalid live update filter");
            throw new CoflnetException("invalid_filter", e.Message);
        }
    }

    /// <summary>Removes the current subscription of the given connection (if any).</summary>
    public void Unsubscribe(ILiveUpdateConnection con)
    {
        lock (subLock)
        {
            if (!byConnection.TryRemove(con, out var sub))
                return;
            RemoveFromIndex(con, sub);
            activeSubsGauge.Set(byConnection.Count);
            activeKindGauge.Set(byConnection.Values.Count(v => v.Kind == SubKind.Active));
        }
    }

    /// <summary>Removes a connection from its routing index. Caller must hold <see cref="subLock"/>.</summary>
    private void RemoveFromIndex(ILiveUpdateConnection con, TopicSub sub)
    {
        if (sub == null)
            return;
        if (sub.Kind == SubKind.Active)
        {
            RemoveActive(sub);
            return;
        }
        var target = sub.Kind == SubKind.Sold ? soldSubs : auctionSubs;
        if (target.TryGetValue(sub.Key, out var bag))
        {
            bag.TryRemove(con, out _);
            if (bag.IsEmpty)
                target.TryRemove(sub.Key, out _);
        }
    }

    private void RemoveActive(TopicSub sub)
    {
        activeWildcardSubs.TryRemove(sub, out _);
        if (sub.Tags != null)
            foreach (var tag in sub.Tags)
                if (activeTagSubs.TryGetValue(tag, out var bag))
                {
                    bag.TryRemove(sub, out _);
                    if (bag.IsEmpty)
                        activeTagSubs.TryRemove(new KeyValuePair<string, ConcurrentDictionary<TopicSub, byte>>(tag, bag));
                }
        foreach (var uuid in sub.Close())
            RemoveFromSentIndex(uuid, sub);
    }

    private void RemoveFromSentIndex(string uuid, TopicSub sub)
    {
        if (sentIndex.TryGetValue(uuid, out var bag))
        {
            bag.TryRemove(sub, out _);
            if (bag.IsEmpty)
                sentIndex.TryRemove(new KeyValuePair<string, ConcurrentDictionary<TopicSub, byte>>(uuid, bag));
        }
    }

    /// <summary>Strips UI-only and pagination keys that are not real filters.</summary>
    private static Dictionary<string, string> CleanFilter(Dictionary<string, string> filter)
    {
        var clean = new Dictionary<string, string>();
        if (filter == null)
            return clean;
        foreach (var pair in filter)
        {
            if (pair.Key == null || pair.Key.StartsWith('_') || pair.Key == "page")
                continue;
            clean[pair.Key] = pair.Value;
        }
        return clean;
    }

    private void OnNewBid(SaveAuction auction)
    {
        if (auction?.Uuid == null || !auctionSubs.TryGetValue(auction.Uuid, out var bag) || bag.IsEmpty)
            return;
        string json;
        try
        {
            // resend the whole auction state (same shape as the `auctionDetails` command)
            json = JSON.Stringify(EnchantColorMapper.Instance.AddColors(auction));
        }
        catch (Exception e)
        {
            logger.LogError(e, "serializing bid update for {uuid}", auction.Uuid);
            return;
        }
        foreach (var sub in bag.Values)
        {
            SendTo(sub, "auctionUpdate", json);
        }
        pushCount.WithLabels("bid").Inc();
    }

    private async Task OnSold(SaveAuction auction)
    {
        if (auction?.Tag == null || !soldSubs.TryGetValue(auction.Tag, out var bag) || bag.IsEmpty)
            return;
        var matching = new List<TopicSub>();
        foreach (var sub in bag.Values)
        {
            try
            {
                if (sub.Matcher == null || sub.Matcher(auction))
                    matching.Add(sub);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "matching sold filter for {tag}", auction.Tag);
            }
        }
        if (matching.Count == 0)
            return;

        string name = null;
        try
        {
            name = await NameResolver(auction.AuctioneerId);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "resolving seller name for {uuid}", auction.Uuid);
        }
        // matches the recent-auction (AuctionPreview) shape the frontend already parses
        var json = JsonConvert.SerializeObject(new
        {
            seller = auction.AuctioneerId,
            price = auction.HighestBidAmount,
            end = auction.End,
            uuid = auction.Uuid,
            playerName = name
        });
        foreach (var sub in matching)
        {
            SendTo(sub, "soldAuction", json);
        }
        pushCount.WithLabels("sold").Inc();
    }

    /// <summary>Pushes a freshly listed auction to all active subscriptions whose tag and filter match.</summary>
    internal async Task OnNewAuction(SaveAuction auction)
    {
        if (auction?.Tag == null || auction.Uuid == null)
            return;
        var candidates = new List<TopicSub>();
        if (activeTagSubs.TryGetValue(auction.Tag, out var bag))
            candidates.AddRange(bag.Keys);
        candidates.AddRange(activeWildcardSubs.Keys);
        if (candidates.Count == 0)
            return;
        var matching = candidates.Where(sub => Matches(sub, auction)).ToList();
        if (matching.Count == 0)
            return;
        var json = await BuildActivePayload(auction);
        foreach (var sub in matching)
        {
            // already known to the client (seeded or pushed before)
            if (!RecordSent(sub, auction.Uuid))
                continue;
            SendTo(sub, "activeAuction", json);
            pushCount.WithLabels("active").Inc();
        }
    }

    /// <summary>Updates the price of already sent auctions on a new bid.</summary>
    internal async Task OnActiveBid(SaveAuction auction)
    {
        if (auction?.Uuid == null || !sentIndex.TryGetValue(auction.Uuid, out var bag) || bag.IsEmpty)
            return;
        var targets = bag.Keys.ToList();
        var json = await BuildActivePayload(auction);
        foreach (var sub in targets)
        {
            SendTo(sub, "activeAuction", json);
            pushCount.WithLabels("activeBid").Inc();
        }
    }

    /// <summary>Tells subscriptions that showed the auction that it is no longer active.</summary>
    internal void OnAuctionGone(SaveAuction auction, string reason)
    {
        if (auction?.Uuid == null || !sentIndex.TryGetValue(auction.Uuid, out var bag) || bag.IsEmpty)
            return;
        var json = JsonConvert.SerializeObject(new { uuid = auction.Uuid, reason });
        foreach (var sub in bag.Keys.ToList())
        {
            sub.Forget(auction.Uuid);
            RemoveFromSentIndex(auction.Uuid, sub);
            SendTo(sub, "auctionGone", json);
            pushCount.WithLabels("gone").Inc();
        }
    }

    private bool Matches(TopicSub sub, SaveAuction auction)
    {
        if (sub.Matcher == null)
            return true;
        try
        {
            return sub.Matcher(auction);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "matching active filter for {tag}", auction.Tag);
            return false;
        }
    }

    /// <summary>Records the uuid for the sub and keeps the reverse index in sync. False if already known or closed.</summary>
    private bool RecordSent(TopicSub sub, string uuid)
    {
        if (!sub.TryRecord(uuid, out var evicted))
            return false;
        if (evicted != null)
            RemoveFromSentIndex(evicted, sub);
        sentIndex.GetOrAdd(uuid, _ => new())[sub] = 0;
        // the sub may have been closed in between, don't leave a stale entry behind
        if (sub.IsClosed)
            RemoveFromSentIndex(uuid, sub);
        return true;
    }

    private async Task<string> BuildActivePayload(SaveAuction auction)
    {
        string name = null;
        try
        {
            name = await NameResolver(auction.AuctioneerId);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "resolving seller name for {uuid}", auction.Uuid);
        }
        // same shape as sky-api's ActiveAuctionPreview
        return JsonConvert.SerializeObject(new
        {
            uuid = auction.Uuid,
            tag = auction.Tag,
            itemName = auction.ItemName,
            tier = auction.Tier.ToString(),
            bin = auction.Bin,
            startingBid = auction.StartingBid,
            highestBidAmount = auction.HighestBidAmount,
            start = auction.Start,
            end = auction.End,
            count = auction.Count,
            auctioneerId = auction.AuctioneerId,
            playerName = name,
            highlights = new Dictionary<string, string>()
        });
    }

    private void SendTo(TopicSub sub, string type, string json)
    {
        try
        {
            sub.Connection.SendBack(new MessageData(type, json) { mId = sub.MId });
        }
        catch (Exception e)
        {
            logger.LogError(e, "sending {type} update", type);
            Unsubscribe(sub.Connection);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.IsNullOrEmpty(config["TOPICS:NEW_BID"]))
            FlipperService.RunIsolatedForever(ConsumeNewBids, "live bid updates", stoppingToken);
        else
            logger.LogWarning("TOPICS:NEW_BID not configured, live bid updates disabled");

        if (SoldTopics.Length > 0)
            FlipperService.RunIsolatedForever(ConsumeSold, "live sold updates", stoppingToken);
        else
            logger.LogWarning("no sold auction topics configured, live sold updates disabled");

        var activeConsumers = new (string Key, string Suffix, Func<SaveAuction, Task> Handler)[]
        {
            ("NEW_AUCTION", "new", OnNewAuction),
            ("NEW_BID", "bid", OnActiveBid),
            ("SOLD_AUCTION", "sold", a => { OnAuctionGone(a, "sold"); return Task.CompletedTask; }),
            ("AUCTION_ENDED", "ended", a => { OnAuctionGone(a, "ended"); return Task.CompletedTask; }),
            ("MISSING_AUCTION", "cancelled", a => { OnAuctionGone(a, "cancelled"); return Task.CompletedTask; }),
        };
        foreach (var (key, suffix, handler) in activeConsumers)
        {
            var topic = config["TOPICS:" + key];
            if (string.IsNullOrEmpty(topic))
            {
                logger.LogWarning("TOPICS:{key} not configured, live active auction updates for it disabled", key);
                continue;
            }
            FlipperService.RunIsolatedForever(t => ConsumeActive(topic, suffix, handler, t), "live active " + suffix, stoppingToken);
        }
        return Task.CompletedTask;
    }

    private Task ConsumeActive(string topic, string suffix, Func<SaveAuction, Task> handler, CancellationToken token)
    {
        return KafkaConsumer.ConsumeBatch<SaveAuction>(config, new string[] { topic }, async batch =>
        {
            foreach (var auction in batch)
            {
                try
                {
                    await handler(auction);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "processing active auction update {suffix}", suffix);
                }
            }
        }, token, GroupId + "-active-" + suffix, 50, AutoOffsetReset.Latest);
    }

    private Task ConsumeNewBids(CancellationToken token)
    {
        var topic = config["TOPICS:NEW_BID"];
        return KafkaConsumer.ConsumeBatch<SaveAuction>(config, new string[] { topic }, batch =>
        {
            foreach (var auction in batch)
            {
                try
                {
                    OnNewBid(auction);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "processing new bid");
                }
            }
            return Task.CompletedTask;
        }, token, GroupId, 50, AutoOffsetReset.Latest);
    }

    private string[] SoldTopics => new string[] { config["TOPICS:SOLD_AUCTION"], config["TOPICS:AUCTION_ENDED"] }
        .Where(t => !string.IsNullOrEmpty(t)).ToArray();

    private Task ConsumeSold(CancellationToken token)
    {
        return KafkaConsumer.ConsumeBatch<SaveAuction>(config, SoldTopics, async batch =>
        {
            foreach (var auction in batch)
            {
                try
                {
                    await OnSold(auction);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "processing sold auction");
                }
            }
        }, token, GroupId, 50, AutoOffsetReset.Latest);
    }

    // unique group per instance so every pod receives all events (mirrors FlipperService)
    private static string GroupId => System.Net.Dns.GetHostName() + "-liveupdates";

    internal const int MaxActiveTags = 8;
    internal const int MaxKnownUuids = 200;
    internal const int MaxSentUuids = 2000;

    internal enum SubKind { Auction, Sold, Active }

    internal class TopicSub
    {
        public ILiveUpdateConnection Connection { get; }
        public long MId { get; }
        /// <summary>Routing key: the auction uuid for bids, the item tag for sold, the tag list for active.</summary>
        public string Key { get; }
        public SubKind Kind { get; }
        public Func<SaveAuction, bool> Matcher { get; }
        /// <summary>Item tags of an active subscription, null for the wildcard-less other kinds.</summary>
        public string[] Tags { get; }
        public bool IsWildcard => Tags != null && Tags.Length == 1 && Tags[0] == "*";

        private readonly object sentLock = new();
        private readonly Queue<string> sentOrder = new();
        private readonly HashSet<string> sent = new();
        private bool closed;

        public bool IsClosed { get { lock (sentLock) return closed; } }

        public TopicSub(ILiveUpdateConnection connection, long mId, string key, SubKind kind, Func<SaveAuction, bool> matcher, string[] tags = null)
        {
            Connection = connection;
            MId = mId;
            Key = key;
            Kind = kind;
            Matcher = matcher;
            Tags = tags;
        }

        /// <summary>Adds the uuid to the bounded FIFO set. False if it was known already or the sub is closed.</summary>
        public bool TryRecord(string uuid, out string evicted)
        {
            evicted = null;
            lock (sentLock)
            {
                if (closed || !sent.Add(uuid))
                    return false;
                sentOrder.Enqueue(uuid);
                if (sentOrder.Count > MaxSentUuids)
                {
                    evicted = sentOrder.Dequeue();
                    sent.Remove(evicted);
                }
                return true;
            }
        }

        public void Forget(string uuid)
        {
            lock (sentLock)
            {
                // the queue entry is left behind and removed lazily when it reaches the front
                sent.Remove(uuid);
            }
        }

        public string[] SnapshotSent()
        {
            lock (sentLock)
                return sent.ToArray();
        }

        /// <summary>Marks the sub closed and returns the uuids that were indexed.</summary>
        public string[] Close()
        {
            lock (sentLock)
            {
                closed = true;
                var all = sent.ToArray();
                sent.Clear();
                sentOrder.Clear();
                return all;
            }
        }
    }
}
