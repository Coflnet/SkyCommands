using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using hypixel.Flipper;

namespace hypixel
{

    /// <summary>
    /// Frontendfacing methods for the flipper
    /// </summary>
    public class FlipperService
    {
        public static FlipperService Instance = new FlipperService();

        private ConcurrentDictionary<long, int> Subs = new ConcurrentDictionary<long, int>();
        private ConcurrentDictionary<long, int> SlowSubs = new ConcurrentDictionary<long, int>();
        public ConcurrentQueue<FlipInstance> Flipps = new ConcurrentQueue<FlipInstance>();
        private ConcurrentQueue<FlipInstance> SlowFlips = new ConcurrentQueue<FlipInstance>();
        /// <summary>
        /// Wherether or not a given <see cref="SaveAuction.UId"/> was a flip or not
        /// </summary>
        private ConcurrentDictionary<long, bool> FlipIdLookup = new ConcurrentDictionary<long, bool>();
        public static readonly string ConsumeTopic = SimplerConfig.Config.Instance["TOPICS:FLIP_CONSUME"];

        private const string FoundFlippsKey = "foundFlipps";

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
        public void AddConnection(SkyblockBackEnd con, int id = 0)
        {
            Subs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            var toSendFlips = Flipps.Reverse().Take(5);
            SendFlipHistory(con, id, toSendFlips);
        }

        public void AddNonConnection(SkyblockBackEnd con, int id = 0)
        {
            SlowSubs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            SendFlipHistory(con, id, LoadBurst, 0);
        }

        public void RemoveNonConnection(SkyblockBackEnd con)
        {
            SlowSubs.TryRemove(con.Id, out int value);
        }

        public void RemoveConnection(SkyblockBackEnd con)
        {
            Subs.TryRemove(con.Id, out int value);
            RemoveNonConnection(con);
        }




        private static void SendFlipHistory(SkyblockBackEnd con, int id, IEnumerable<FlipInstance> toSendFlips, int delay = 5000)
        {
            Task.Run(async () =>
            {
                foreach (var item in toSendFlips)
                {
                    var data = CreateDataFromFlip(item);
                    data.mId = id;
                    con.SendBack(data);
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
            var message = new MessageData("sold", auctionUUid);
            NotifyAll(message, Subs);
            NotifyAll(message, SlowSubs);
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
            MessageData message = CreateDataFromFlip(flip);
            Console.Write("d flips");
            NotifyAll(message, Subs);
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


        private static MessageData CreateDataFromFlip(FlipInstance flip)
        {
            return new MessageData("flip", JSON.Stringify(flip), 60);
        }

        private static void NotifyAll(MessageData message, ConcurrentDictionary<long, int> subscribers)
        {
            foreach (var item in subscribers.Keys)
            {
                var m = MessageData.Copy(message);
                m.mId = subscribers[item];
                try
                {
                    if (!SkyblockBackEnd.SendTo(m, item))
                        subscribers.TryRemove(item, out int value);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send flip {e.Message} {e.StackTrace}");
                    subscribers.TryRemove(item, out int value);
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
                    var message = CreateDataFromFlip(flip);
                    Console.WriteLine("\nshouting slow flip");
                    NotifyAll(message, SlowSubs);
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
            string[] topics = new string[] { Indexer.AuctionEndedTopic, Indexer.SoldAuctionTopic, Indexer.MissingAuctionsTopic };
            ConsumeBatch<SaveAuction>(topics, AuctionSold);
            return Task.CompletedTask;
        }

        public Task ListenToNewFlips()
        {

            TryLoadFromCache();
            string[] topics = new string[] { ConsumeTopic };
            ConsumeBatch<FlipInstance>(topics, DeliverFlip);
            return Task.CompletedTask;
        }

        private void ConsumeBatch<T>(string[] topics, Action<T> work)
        {
            using (var c = new ConsumerBuilder<Ignore, T>(consumerConf).SetValueDeserializer(SerializerFactory.GetDeserializer<T>()).Build())
            {
                c.Subscribe(topics);
                try
                {
                    var batch = new List<TopicPartitionOffset>();
                    while (true)
                    {
                        try
                        {
                            var cr = c.Consume(500);
                            if (cr == null)
                                continue;
                            if (cr.TopicPartitionOffset.Offset % 20 == 0)
                                Console.WriteLine($"consumed {cr.TopicPartitionOffset.Topic} {cr.TopicPartitionOffset.Offset}");
                            work(cr.Message.Value);
                            batch.Add(cr.TopicPartitionOffset);
                        }
                        catch (ConsumeException e)
                        {
                            dev.Logger.Instance.Error(e, "flipper consume batch " + topics[0]);
                        }
                        if (batch.Count > 10)
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
}
