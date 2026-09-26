using System.Threading;
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
    }
}
