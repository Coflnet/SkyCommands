using System.Threading.Tasks;
using Coflnet.Sky.FlipTracker.Client.Api;
using Confluent.Kafka;

namespace Coflnet.Sky.Commands
{
    public class FlipTrackingService
    {
        public TrackerApi flipTracking;

        public static FlipTrackingService Instance = new FlipTrackingService();

        private static string ProduceTopic;
        private static ProducerConfig producerConfig;

        IProducer<string, FlipTracker.Client.Model.FlipEvent> producer;

        static FlipTrackingService()
        {
            producerConfig = new ProducerConfig { BootstrapServers = SimplerConfig.Config.Instance["KAFKA_HOST"], CancellationDelayMaxMs = 1000 };
            ProduceTopic = SimplerConfig.Config.Instance["TOPICS:FLIP_EVENT"];
        }

        public FlipTrackingService()
        {
            producer = new ProducerBuilder<string, FlipTracker.Client.Model.FlipEvent>(new ProducerConfig { 
                    BootstrapServers = SimplerConfig.Config.Instance["KAFKA_HOST"], 
                    CancellationDelayMaxMs = 1000 })
                    .SetValueSerializer(hypixel.SerializerFactory.GetSerializer<FlipTracker.Client.Model.FlipEvent>()).Build();
            flipTracking = new TrackerApi("http://" + SimplerConfig.Config.Instance["FLIPTRACKER_HOST"]);
        }


        public async Task ReceiveFlip(string auctionId, string playerId)
        {
            try
            {
                await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_1);
            }
            catch (System.Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                throw e;
            }
        }
        public async Task ClickFlip(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_2);
        }
        public async Task PurchaseStart(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_4);
        }
        public async Task PurchaseConfirm(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_8);
        }
        public async Task Sold(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_16);
        }
        public async Task UpVote(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_32);
        }
        public async Task DownVote(string auctionId, string playerId)
        {
            await SendEvent(auctionId, playerId, FlipTracker.Client.Model.FlipEventType.NUMBER_64);
        }

        private async Task SendEvent(string auctionId, string playerId, FlipTracker.Client.Model.FlipEventType type)
        {
            var flipEvent = new FlipTracker.Client.Model.FlipEvent()
            {
                Type = type,
                PlayerId = hypixel.AuctionService.Instance.GetId(playerId),
                AuctionId = hypixel.AuctionService.Instance.GetId(auctionId),
                Timestamp = System.DateTime.Now
            };

            producer.Produce(ProduceTopic, new Message<string, FlipTracker.Client.Model.FlipEvent>() { Value = flipEvent });

        }

        public async Task NewFlip(LowPricedAuction flip)
        {
            var res = await flipTracking.TrackerFlipAuctionIdPostAsync(flip.Auction.Uuid, new FlipTracker.Client.Model.Flip()
            {
                FinderType = (FlipTracker.Client.Model.FinderType?)flip.Finder,
                TargetPrice = flip.TargetPrice
            });
        }
    }
}