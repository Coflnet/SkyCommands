using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Filter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Coflnet.Sky.Commands;

public class LiveUpdateServiceTests
{
    private class FakeConnection : ILiveUpdateConnection
    {
        public List<MessageData> Sent = new();
        public Action Closed;
        public void SendBack(MessageData data) => Sent.Add(data);
        public void RegisterCloseHandler(Action onClose) => Closed += onClose;
        public void Close() => Closed?.Invoke();
        public List<MessageData> Of(string type) => Sent.Where(m => m.Type == type).ToList();
    }

    private LiveUpdateService service;
    private FakeConnection con;

    [SetUp]
    public void Setup()
    {
        service = new LiveUpdateService(new ConfigurationBuilder().Build(), new FilterEngine(null), NullLogger<LiveUpdateService>.Instance);
        service.NameResolver = id => Task.FromResult("name-" + id);
        con = new FakeConnection();
    }

    private static SaveAuction Auction(string uuid, string tag, bool bin = true) => new()
    {
        Uuid = uuid,
        Tag = tag,
        ItemName = "Item " + tag,
        Bin = bin,
        StartingBid = 100,
        HighestBidAmount = 150,
        Count = 1,
        Tier = Tier.EPIC,
        AuctioneerId = "seller",
        Start = new DateTime(2029, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        End = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static readonly Dictionary<string, string> BinFilter = new() { { "Bin", "true" } };

    [Test]
    public async Task ActiveTagsAndFilterRouting()
    {
        service.Subscribe(con, 5, "active/a, b", BinFilter);

        await service.OnNewAuction(Auction("u1", "A"));
        await service.OnNewAuction(Auction("u2", "C"));
        await service.OnNewAuction(Auction("u3", "B", bin: false));

        var pushed = con.Of("activeAuction");
        Assert.That(pushed.Count, Is.EqualTo(1));
        Assert.That(pushed[0].mId, Is.EqualTo(5));
        var json = JObject.Parse(pushed[0].Data);
        Assert.That((string)json["uuid"], Is.EqualTo("u1"));
        Assert.That((string)json["tag"], Is.EqualTo("A"));
        Assert.That((string)json["tier"], Is.EqualTo("EPIC"));
        Assert.That(((DateTime)json["start"]).ToUniversalTime(), Is.EqualTo(new DateTime(2029, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That((string)json["playerName"], Is.EqualTo("name-seller"));
        Assert.That(json["highlights"], Is.InstanceOf<JObject>());
    }

    [Test]
    public void WildcardWithoutFilterRejected()
    {
        var e = Assert.Throws<CoflnetException>(() => service.Subscribe(con, 1, "active/*", new Dictionary<string, string>()));
        Assert.That(e.Slug, Is.EqualTo("filter_required"));
        // ui only keys do not count as filter
        e = Assert.Throws<CoflnetException>(() => service.Subscribe(con, 1, "active/*", new() { { "_x", "1" }, { "page", "2" } }));
        Assert.That(e.Slug, Is.EqualTo("filter_required"));
    }

    [Test]
    public async Task WildcardWithFilterReceivesAnyTag()
    {
        service.Subscribe(con, 1, "active/*", BinFilter);
        await service.OnNewAuction(Auction("u1", "X"));
        await service.OnNewAuction(Auction("u2", "Y"));
        await service.OnNewAuction(Auction("u3", "Z", bin: false));
        Assert.That(con.Of("activeAuction").Count, Is.EqualTo(2));
    }

    [Test]
    public void InvalidFilterRejectedForActiveAndSold()
    {
        var bad = new Dictionary<string, string> { { "NoSuchFilterXyz", "1" } };
        Assert.That(Assert.Throws<CoflnetException>(() => service.Subscribe(con, 1, "active/A", bad)).Slug, Is.EqualTo("invalid_filter"));
        Assert.That(Assert.Throws<CoflnetException>(() => service.Subscribe(con, 2, "sold/A", bad)).Slug, Is.EqualTo("invalid_filter"));
    }

    [Test]
    public void TooManyTagsRejected()
    {
        var ok = string.Join(",", Enumerable.Range(0, 8).Select(i => "T" + i));
        service.Subscribe(con, 1, "active/" + ok, null);
        var e = Assert.Throws<CoflnetException>(() => service.Subscribe(con, 2, "active/" + ok + ",T9", null));
        Assert.That(e.Slug, Is.EqualTo("too_many_tags"));
    }

    [TestCase("sold", "sold")]
    [TestCase("ended", "ended")]
    [TestCase("cancelled", "cancelled")]
    public async Task GonePushedForSentUuid(string reason, string expected)
    {
        service.Subscribe(con, 3, "active/A", null);
        await service.OnNewAuction(Auction("u1", "A"));
        service.OnAuctionGone(Auction("unknown", "A"), reason);
        Assert.That(con.Of("auctionGone"), Is.Empty);

        service.OnAuctionGone(Auction("u1", "A"), reason);
        var gone = con.Of("auctionGone");
        Assert.That(gone.Count, Is.EqualTo(1));
        Assert.That(gone[0].mId, Is.EqualTo(3));
        Assert.That(gone[0].Data, Is.EqualTo($"{{\"uuid\":\"u1\",\"reason\":\"{expected}\"}}"));
        // only once
        service.OnAuctionGone(Auction("u1", "A"), reason);
        Assert.That(con.Of("auctionGone").Count, Is.EqualTo(1));
    }

    [Test]
    public void KnownUuidsSeedGone()
    {
        service.Subscribe(con, 1, "active/A", null, new[] { "k1", "k2" });
        service.OnAuctionGone(Auction("k2", "A"), "sold");
        Assert.That(con.Of("auctionGone").Count, Is.EqualTo(1));
    }

    [Test]
    public async Task KnownUuidsCappedAndNotPushedAgain()
    {
        var known = Enumerable.Range(0, 250).Select(i => "k" + i).ToList();
        service.Subscribe(con, 1, "active/A", null, known);
        service.OnAuctionGone(Auction("k249", "A"), "sold");
        Assert.That(con.Of("auctionGone"), Is.Empty);
        await service.OnNewAuction(Auction("k1", "A"));
        Assert.That(con.Of("activeAuction"), Is.Empty);
    }

    [Test]
    public async Task BidOnlyForSentUuids()
    {
        service.Subscribe(con, 1, "active/A", null);
        await service.OnActiveBid(Auction("u1", "A"));
        Assert.That(con.Of("activeAuction"), Is.Empty);
        await service.OnNewAuction(Auction("u1", "A"));
        var updated = Auction("u1", "A");
        updated.HighestBidAmount = 999;
        await service.OnActiveBid(updated);
        var pushes = con.Of("activeAuction");
        Assert.That(pushes.Count, Is.EqualTo(2));
        Assert.That((long)JObject.Parse(pushes[1].Data)["highestBidAmount"], Is.EqualTo(999));
    }

    [Test]
    public async Task NewerSubscribeReplacesOlder()
    {
        service.Subscribe(con, 1, "active/A", null);
        service.Subscribe(con, 2, "active/B", null);
        // older reordered subscribe is ignored
        service.Subscribe(con, 1, "active/A", null);
        await service.OnNewAuction(Auction("u1", "A"));
        await service.OnNewAuction(Auction("u2", "B"));
        var pushes = con.Of("activeAuction");
        Assert.That(pushes.Count, Is.EqualTo(1));
        Assert.That(pushes[0].mId, Is.EqualTo(2));
    }

    [Test]
    public async Task UnsubscribeAndCloseCleanIndex()
    {
        service.Subscribe(con, 1, "active/A", null);
        await service.OnNewAuction(Auction("u1", "A"));
        service.Unsubscribe(con);
        service.OnAuctionGone(Auction("u1", "A"), "sold");
        await service.OnNewAuction(Auction("u2", "A"));
        Assert.That(con.Of("auctionGone"), Is.Empty);
        Assert.That(con.Of("activeAuction").Count, Is.EqualTo(1));

        var con2 = new FakeConnection();
        service.Subscribe(con2, 1, "active/A", null, new[] { "k" });
        con2.Close();
        service.OnAuctionGone(Auction("k", "A"), "sold");
        Assert.That(con2.Of("auctionGone"), Is.Empty);
    }

    [Test]
    public async Task ResubscribeDropsOldSentIndex()
    {
        service.Subscribe(con, 1, "active/A", null);
        await service.OnNewAuction(Auction("u1", "A"));
        service.Subscribe(con, 2, "active/B", null);
        service.OnAuctionGone(Auction("u1", "A"), "sold");
        Assert.That(con.Of("auctionGone"), Is.Empty);
    }

    [Test]
    public async Task ActivePageStateKeysAreIgnored()
    {
        service.Subscribe(con, 1, "active/A", new()
        {
            { "Bin", "true" }, { "armorSet", "x" }, { "categories", "y" }, { "tags", "A" }, { "q", "z" },
            { "orderBy", "price" }, { "perTagLimit", "5" }, { "page", "1" }, { "pageSize", "20" }
        });
        await service.OnNewAuction(Auction("u1", "A"));
        await service.OnNewAuction(Auction("u2", "A", bin: false));
        Assert.That(con.Of("activeAuction").Count, Is.EqualTo(1));
    }

    [TestCase("itemNameContains")]
    [TestCase("ItemNameContains")]
    public async Task ItemNameContainsAppliedCaseInsensitive(string key)
    {
        service.Subscribe(con, 1, "active/A", new() { { "Bin", "true" }, { key, "ITEM a" } });
        await service.OnNewAuction(Auction("u1", "A"));
        var other = Auction("u2", "A");
        other.ItemName = "Something";
        await service.OnNewAuction(other);
        var nameless = Auction("u3", "A");
        nameless.ItemName = null;
        await service.OnNewAuction(nameless);
        Assert.That(con.Of("activeAuction").Select(m => (string)JObject.Parse(m.Data)["uuid"]), Is.EqualTo(new[] { "u1" }));
    }

    [Test]
    public async Task WildcardFilterRequiredRules()
    {
        var e = Assert.Throws<CoflnetException>(() => service.Subscribe(con, 1, "active/*",
            new() { { "armorSet", "x" }, { "q", "y" }, { "page", "1" }, { "pageSize", "5" }, { "tags", "A" } }));
        Assert.That(e.Slug, Is.EqualTo("filter_required"));
        // name search alone is a filter
        service.Subscribe(con, 2, "active/*", new() { { "itemNameContains", "item" } });
        await service.OnNewAuction(Auction("u1", "Q"));
        Assert.That(con.Of("activeAuction").Count, Is.EqualTo(1));
    }
}
