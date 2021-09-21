using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet.Sky.Commands;
using Coflnet.Sky.Filter;
using Confluent.Kafka;
using OpenTracing.Propagation;

namespace hypixel
{

    /// <summary>
    /// Frontendfacing methods for the flipper
    /// </summary>
    public class FlipperService
    {
        public static FlipperService Instance = new FlipperService();

        private ConcurrentDictionary<long, IFlipConnection> Subs = new ConcurrentDictionary<long, IFlipConnection>();
        private ConcurrentDictionary<long, IFlipConnection> SlowSubs = new ConcurrentDictionary<long, IFlipConnection>();
        private ConcurrentDictionary<long, IFlipConnection> SuperSubs = new ConcurrentDictionary<long, IFlipConnection>();
        public ConcurrentQueue<FlipInstance> Flipps = new ConcurrentQueue<FlipInstance>();
        private ConcurrentQueue<FlipInstance> SlowFlips = new ConcurrentQueue<FlipInstance>();
        /// <summary>
        /// Wherether or not a given <see cref="SaveAuction.UId"/> was a flip or not
        /// </summary>
        private ConcurrentDictionary<long, bool> FlipIdLookup = new ConcurrentDictionary<long, bool>();
        public static readonly string ConsumeTopic = SimplerConfig.Config.Instance["TOPICS:FLIP"];
        public static readonly string SettingsTopic = SimplerConfig.Config.Instance["TOPICS:SETTINGS_CHANGE"];
        private static ProducerConfig producerConfig = new ProducerConfig { BootstrapServers = SimplerConfig.Config.Instance["KAFKA_HOST"] };

        private const string FoundFlippsKey = "foundFlipps";

        static Prometheus.Histogram runtroughTime = Prometheus.Metrics.CreateHistogram("sky_commands_auction_to_flip_seconds", "Represents the time in seconds taken from loading the auction to sendingthe flip. (should be close to 0)",
            new Prometheus.HistogramConfiguration()
            {
                Buckets = Prometheus.Histogram.LinearBuckets(start: 1, width: 2, count: 10)
            });

        /// <summary>
        /// Special load burst queue that will send out 5 flips at load
        /// </summary>
        private Queue<FlipInstance> LoadBurst = new Queue<FlipInstance>();
        private ConcurrentDictionary<long, DateTime> SoldAuctions = new ConcurrentDictionary<long, DateTime>();

        private async Task TryLoadFromCache()
        {
            if (Flipps.Count == 0)
            {
                // try to get from redis

                var fromCache = await CacheService.Instance.GetFromRedis<ConcurrentQueue<FlipInstance>>(FoundFlippsKey);
                if (fromCache != default(ConcurrentQueue<FlipInstance>))
                {
                    Flipps = fromCache;
                    foreach (var item in Flipps)
                    {
                        FlipIdLookup[item.UId] = true;
                    }
                }
            }
        }


        internal async Task<DeliveryResult<string, SettingsChange>> UpdateSettings(SettingsChange settings)
        {
            using (var p = new ProducerBuilder<string, SettingsChange>(producerConfig).SetValueSerializer(SerializerFactory.GetSerializer<SettingsChange>()).Build())
            {
                return await p.ProduceAsync(SettingsTopic, new Message<string, SettingsChange> { Value = settings });
            }

        }

        public void AddConnection(IFlipConnection con)
        {
            Subs.AddOrUpdate(con.Id, cid => con, (cid, oldMId) => con);
            var toSendFlips = Flipps.Reverse().Take(25);
            SendFlipHistory(con, toSendFlips, 0);
        }

        public void AddNonConnection(IFlipConnection con)
        {
            SlowSubs.AddOrUpdate(con.Id, cid => con, (cid, oldMId) => con);
            SendFlipHistory(con, LoadBurst, 0);
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                Console.WriteLine("Added new con " + SlowSubs.Count);
            });
        }

        public void RemoveNonConnection(IFlipConnection con)
        {
            SlowSubs.TryRemove(con.Id, out IFlipConnection value);
        }

        public void RemoveConnection(IFlipConnection con)
        {
            Subs.TryRemove(con.Id, out IFlipConnection value);
            RemoveNonConnection(con);
        }




        private static void SendFlipHistory(IFlipConnection con, IEnumerable<FlipInstance> toSendFlips, int delay = 5000)
        {
            Task.Run(async () =>
            {
                foreach (var item in toSendFlips)
                {
                    con.SendFlip(item);

                    await Task.Delay(delay);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Tell the flipper that an auction was sold
        /// </summary>
        /// <param name="auction"></param>
        public void AuctionSold(SaveAuction auction)
        {
            if (!FlipIdLookup.ContainsKey(auction.UId))
                return;
            SoldAuctions[auction.UId] = auction.End;
            var auctionUUid = auction.Uuid;
            NotifySubsInactiveAuction(auctionUUid);
        }

        private void NotifySubsInactiveAuction(string auctionUUid)
        {
            var inacive = new List<long>();
            foreach (var item in Subs)
            {
                if (!item.Value.SendSold(auctionUUid))
                    inacive.Add(item.Key);
            }
            foreach (var item in SlowSubs)
            {
                if (!item.Value.SendSold(auctionUUid))
                    inacive.Add(item.Key);
            }

            foreach (var item in inacive)
            {
                SlowSubs.TryRemove(item, out IFlipConnection con);
                Subs.TryRemove(item, out con);
            }
        }

        /// <summary>
        /// Auction is no longer active for some reason
        /// </summary>
        /// <param name="uuid"></param>
        public void AuctionInactive(string uuid)
        {
            NotifySubsInactiveAuction(uuid);
            var uid = AuctionService.Instance.GetId(uuid);
            SoldAuctions[uid] = DateTime.Now;
        }



        /// <summary>
        /// Sends out new flips based on tier.
        /// (active on the light client)
        /// </summary>
        /// <param name="flip"></param>
        private void DeliverFlip(FlipInstance flip)
        {
            var tracer = OpenTracing.Util.GlobalTracer.Instance;
            var span = OpenTracing.Util.GlobalTracer.Instance.BuildSpan("SendFlip")
                    .AsChildOf(tracer.Extract(BuiltinFormats.TextMap, flip.Auction.TraceContext));
            using var scope = span.StartActive();

            NotifyAll(flip, Subs);
            SlowFlips.Enqueue(flip);
            Flipps.Enqueue(flip);
            FlipIdLookup[flip.UId] = true;
            if (Flipps.Count > 1500)
            {
                if (Flipps.TryDequeue(out FlipInstance result))
                {
                    FlipIdLookup.Remove(result.UId, out bool value);
                }
            }
        }



        private static void NotifyAll(FlipInstance flip, ConcurrentDictionary<long, IFlipConnection> subscribers)
        {
            runtroughTime.Observe((DateTime.Now - flip.Auction.FindTime).TotalSeconds);
            foreach (var item in subscribers.Keys)
            {
                try
                {
                    if (!subscribers[item].SendFlip(flip))
                        subscribers.TryRemove(item, out IFlipConnection value);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send flip {e.Message} {e.StackTrace}");
                    subscribers.TryRemove(item, out IFlipConnection value);
                }
            }
        }



        public async Task ProcessSlowQueue()
        {
            try
            {
                if (SlowFlips.TryDequeue(out FlipInstance flip))
                {
                    if (SoldAuctions.ContainsKey(flip.UId))
                        flip.Sold = true;
                    NotifyAll(flip, SlowSubs);
                    if (flip.Uuid[0] == 'a')
                        Console.Write("sf+" + SlowSubs.Count);
                    LoadBurst.Enqueue(flip);
                    if (LoadBurst.Count > 5)
                        LoadBurst.Dequeue();
                }

                await Task.Delay(DelayTimeFor(SlowFlips.Count) * 4 / 5);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "slow queue processor");
            }
        }

        ConsumerConfig consumerConf = new ConsumerConfig
        {
            GroupId = System.Net.Dns.GetHostName(),
            BootstrapServers = Program.KafkaHost,
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        public Task ListentoUnavailableTopics()
        {
            Console.WriteLine("listening to unavailibily topics");
            string[] topics = new string[] { Indexer.AuctionEndedTopic, Indexer.SoldAuctionTopic, Indexer.MissingAuctionsTopic };
            ConsumeBatch<SaveAuction>(topics, AuctionSold);
            return Task.CompletedTask;
        }

        public async Task ListenToNewFlips()
        {

            await TryLoadFromCache();
            string[] topics = new string[] { ConsumeTopic };

            Console.WriteLine("starting to listen for new auctions via topic " + ConsumeTopic);
            ConsumeBatch<FlipInstance>(topics, DeliverFlip);
            Console.WriteLine("ended listening");
        }

        public Task ListenForSettingsChange()
        {
            string[] topics = new string[] { SettingsTopic };

            Console.WriteLine("starting to listen for config changes topic " + SettingsTopic);
            ConsumeBatch<SettingsChange>(topics, UpdateSettingsInternal);
            return Task.CompletedTask;
        }

        private void UpdateSettingsInternal(SettingsChange settings)
        {
            foreach (var item in settings.ConIds)
            {
                if (SlowSubs.TryGetValue(item, out IFlipConnection con)
                    || Subs.TryGetValue(item, out con)
                    || SuperSubs.TryGetValue(item, out con))
                {
                    con.UpdateSettings(settings);
                }
            }
            foreach (var item in SkyblockBackEnd.GetConnectionsOfUser(settings.UserId))
            {
                item.UpdateSettings(settings);
            }
            Console.WriteLine("settings update: " + JSON.Stringify(settings));
        }

        private void ConsumeBatch<T>(string[] topics, Action<T> work, int batchSize = 10)
        {
            using (var c = new ConsumerBuilder<Ignore, T>(consumerConf).SetValueDeserializer(SerializerFactory.GetDeserializer<T>()).Build())
            {
                c.Subscribe(topics);
                try
                {
                    var batch = new List<TopicPartitionOffset>();
                    Console.WriteLine("subscribed to " + string.Join(",", topics));
                    while (true)
                    {
                        try
                        {
                            var cr = c.Consume(500);
                            if (cr == null)
                                continue;
                            if (cr.TopicPartitionOffset.Offset % 200 == 0)
                                Console.WriteLine($"consumed {cr.TopicPartitionOffset.Topic} {cr.TopicPartitionOffset.Offset}");
                            work(cr.Message.Value);
                            batch.Add(cr.TopicPartitionOffset);
                        }
                        catch (ConsumeException e)
                        {
                            dev.Logger.Instance.Error(e, "flipper consume batch " + topics[0]);
                        }
                        if (batch.Count > batchSize)
                        {
                            // tell kafka that we stored the batch
                            c.Commit(batch);
                            batch.Clear();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ensure the consumer leaves the group cleanly and final offsets are committed.
                    c.Close();
                }
            }
        }

        public static int DelayTimeFor(int queueSize)
        {
            return (int)Math.Min((TimeSpan.FromMinutes(5) / (Math.Max(queueSize, 1))).TotalMilliseconds, 10000);
        }

        /// <summary>
        /// Removes old <see cref="SoldAuctions"/>
        /// </summary>
        private void ClearSoldBuffer()
        {
            var toRemove = new List<long>();
            var oldestTime = DateTime.Now - TimeSpan.FromMinutes(10);
            foreach (var item in SoldAuctions)
            {
                if (item.Value < oldestTime)
                    toRemove.Add(item.Key);
            }
            foreach (var item in toRemove)
            {
                SoldAuctions.TryRemove(item, out DateTime deleted);
            }
        }
    }


    [DataContract]
    public class SettingsChange
    {
        [DataMember(Name = "settings")]
        public FlipSettings Settings = new FlipSettings();
        [DataMember(Name = "userId")]
        public int UserId;
        [DataMember(Name = "mcIds")]
        public List<string> McIds = new List<string>();

        [DataMember(Name = "conIds")]
        public List<long> ConIds = new List<long>();


        [DataMember(Name = "tier")]
        public AccountTier Tier;


    }

    public enum AccountTier
    {
        NONE,
        PREMIUM,
        PREMIUM_PLUS,
        SUPER_PREMIUM = 4
    }

    [DataContract]
    public class FlipInstance
    {
        [DataMember(Name = "median")]
        public int MedianPrice;
        [DataMember(Name = "cost")]
        public int LastKnownCost;
        [DataMember(Name = "uuid")]
        public string Uuid;
        [DataMember(Name = "name")]
        public string Name;
        [DataMember(Name = "sellerName")]
        public string SellerName;
        [DataMember(Name = "volume")]
        public float Volume;
        [DataMember(Name = "tag")]
        public string Tag;
        [DataMember(Name = "bin")]
        public bool Bin;
        [DataMember(Name = "sold")]
        public bool Sold { get; set; }
        [DataMember(Name = "tier")]
        public Tier Rarity { get; set; }
        [DataMember(Name = "prop")]
        public List<string> Interesting { get; set; }
        [DataMember(Name = "secondLowestBin")]
        public long? SecondLowestBin { get; set; }

        [DataMember(Name = "lowestBin")]
        public long? LowestBin;
        [DataMember(Name = "auction")]
        public SaveAuction Auction;
        [IgnoreDataMember]
        public long UId => AuctionService.Instance.GetId(this.Uuid);
    }
}
