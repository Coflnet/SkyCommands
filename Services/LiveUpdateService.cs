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
/// </summary>
public class LiveUpdateService : BackgroundService
{
    private readonly IConfiguration config;
    private readonly FilterEngine filterEngine;
    private readonly ILogger<LiveUpdateService> logger;

    /// <summary>The single active subscription per connection (also carries the generation <c>mId</c>).</summary>
    private readonly ConcurrentDictionary<SkyblockBackEnd, TopicSub> byConnection = new();
    /// <summary>Bid-update subscriptions keyed by auction uuid (for O(1) routing).</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<SkyblockBackEnd, TopicSub>> auctionSubs = new();
    /// <summary>Sold-auction subscriptions keyed by item tag (for O(1) routing).</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<SkyblockBackEnd, TopicSub>> soldSubs = new();
    /// <summary>Serializes the multi-step (un)subscribe so a connection can never end up in two indexes.</summary>
    private readonly object subLock = new();

    private static readonly Prometheus.Counter subscribeCount = Prometheus.Metrics
        .CreateCounter("sky_commands_liveupdate_subscribe", "How many live update subscriptions were created");
    private static readonly Prometheus.Counter pushCount = Prometheus.Metrics
        .CreateCounter("sky_commands_liveupdate_push", "How many live updates were pushed", "kind");
    private static readonly Prometheus.Gauge activeSubs = Prometheus.Metrics
        .CreateGauge("sky_commands_liveupdate_active", "How many connections have an active live update subscription");

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
    /// <param name="topic">topic string, e.g. <c>auction/{uuid}</c> or <c>sold/{tag}</c></param>
    /// <param name="filter">optional filter (only used for <c>sold/*</c> topics)</param>
    public void Subscribe(SkyblockBackEnd con, long mId, string topic, Dictionary<string, string> filter)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new CoflnetException("invalid_topic", "A `topic` is required to subscribe");
        var slashIndex = topic.IndexOf('/');
        if (slashIndex <= 0 || slashIndex >= topic.Length - 1)
            throw new CoflnetException("invalid_topic", "The `topic` has to be of the form `auction/{uuid}` or `sold/{tag}`");
        var kind = topic[..slashIndex];
        var value = topic[(slashIndex + 1)..];

        bool isSold;
        Func<SaveAuction, bool> matcher = null;
        switch (kind)
        {
            case "auction":
                isSold = false;
                break;
            case "sold":
                isSold = true;
                var clean = CleanFilter(filter);
                if (clean.Count > 0)
                {
                    try
                    {
                        matcher = filterEngine.GetMatcher(clean);
                    }
                    catch (Exception e)
                    {
                        // an unknown/invalid filter should not break the subscription; fall back to tag only
                        logger.LogWarning(e, "could not build sold filter matcher for tag {tag}", value);
                    }
                }
                break;
            default:
                throw new CoflnetException("invalid_topic", $"Unknown topic kind `{kind}`. Supported: `auction`, `sold`");
        }

        var sub = new TopicSub(con, mId, value, isSold, matcher);
        lock (subLock)
        {
            // messages of one connection are dispatched on independent tasks, so a newer subscribe may
            // have already been applied - never let an older one clobber it (keeps the one-topic invariant)
            if (byConnection.TryGetValue(con, out var current) && current.MId > mId)
                return;
            RemoveFromIndex(con, current);
            byConnection[con] = sub;
            (isSold ? soldSubs : auctionSubs).GetOrAdd(value, _ => new())[con] = sub;
            // clean up when the connection closes (single registration)
            con.OnBeforeClose -= HandleConnectionClose;
            con.OnBeforeClose += HandleConnectionClose;
            activeSubs.Set(byConnection.Count);
        }
        subscribeCount.Inc();
    }

    /// <summary>Removes the current subscription of the given connection (if any).</summary>
    public void Unsubscribe(SkyblockBackEnd con)
    {
        lock (subLock)
        {
            if (!byConnection.TryRemove(con, out var sub))
                return;
            RemoveFromIndex(con, sub);
            activeSubs.Set(byConnection.Count);
        }
    }

    /// <summary>Removes a connection from its routing index. Caller must hold <see cref="subLock"/>.</summary>
    private void RemoveFromIndex(SkyblockBackEnd con, TopicSub sub)
    {
        if (sub == null)
            return;
        var target = sub.IsSold ? soldSubs : auctionSubs;
        if (target.TryGetValue(sub.Key, out var bag))
        {
            bag.TryRemove(con, out _);
            if (bag.IsEmpty)
                target.TryRemove(sub.Key, out _);
        }
    }

    private void HandleConnectionClose(SkyblockBackEnd con) => Unsubscribe(con);

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
            name = await PlayerSearch.Instance.GetNameWithCacheAsync(auction.AuctioneerId);
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
        return Task.CompletedTask;
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

    private class TopicSub
    {
        public SkyblockBackEnd Connection { get; }
        public long MId { get; }
        /// <summary>Routing key: the auction uuid for bids, the item tag for sold.</summary>
        public string Key { get; }
        public bool IsSold { get; }
        public Func<SaveAuction, bool> Matcher { get; }

        public TopicSub(SkyblockBackEnd connection, long mId, string key, bool isSold, Func<SaveAuction, bool> matcher)
        {
            Connection = connection;
            MId = mId;
            Key = key;
            IsSold = isSold;
            Matcher = matcher;
        }
    }
}
