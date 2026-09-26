using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Coflnet.Sky.Commands.Shared;
using NUnit.Framework;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Commands
{
    public class ServerTests
    {
        /// <summary>
        /// Finds a currently unused TCP port by briefly binding to port 0 and reading back
        /// the port the OS assigned. <see cref="HttpServer"/> rejects an explicit port of 0.
        /// </summary>
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void ConfigureSessionCleanup_EnablesSweeperWithLongerWaitTime()
        {
            var server = new HttpServer(GetFreePort());

            Server.ConfigureSessionCleanup(server);

            Assert.That(server.KeepClean, Is.True, "the sweeper must stay enabled or dead sessions leak forever");
            Assert.That(server.WaitTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void ConfigureSessionCleanup_PropagatesToServiceHostsAddedAfterwards()
        {
            var server = new HttpServer(GetFreePort());

            Server.ConfigureSessionCleanup(server);
            server.AddWebSocketService<SkyblockBackEnd>("/skyblock");

            var host = server.WebSocketServices["/skyblock"];

            Assert.That(host.KeepClean, Is.True);
            Assert.That(host.WaitTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        /// <summary>
        /// End to end regression test for the leak: a client's TCP connection is killed abruptly
        /// (no close handshake, matching an app crash or a lost network path), and the server-side
        /// session must not linger. Either SkyblockBackEnd.OnError closing the library session, or
        /// the sweeper reaping it, is an acceptable path - the point is the session dictionary does
        /// not grow forever.
        /// </summary>
        [Test]
        [CancelAfter(20000)]
        public void DeadConnection_DoesNotLingerAsOpenSession()
        {
            DiHandler.OverrideService<FlipperService, FlipperService>(new FlipperService(null, null));

            var port = GetFreePort();
            var server = new HttpServer(port);
            Server.ConfigureSessionCleanup(server);
            server.AddWebSocketService<SkyblockBackEnd>("/skyblock");
            server.Start();

            try
            {
                var host = server.WebSocketServices["/skyblock"];

                using var client = new WebSocket($"ws://127.0.0.1:{port}/skyblock");
                client.Connect();

                WaitUntil(() => host.Sessions.Count == 1, "server never registered the incoming session");

                KillConnectionAbruptly(client);

                WaitUntil(() =>
                {
                    // the sweeper only runs on its own 60s timer in production; call it directly
                    // here so the test does not depend on that interval.
                    host.Sessions.Sweep();
                    return host.Sessions.Count == 0;
                }, "dead session was never removed");

                Assert.That(host.Sessions.Count, Is.EqualTo(0));
            }
            finally
            {
                server.Stop();
            }
        }

        private static void WaitUntil(Func<bool> condition, string failureMessage, int timeoutMs = 10000)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return;
                Thread.Sleep(50);
            }

            Assert.Fail(failureMessage);
        }

        /// <summary>
        /// Forces the client-side TCP connection closed with an RST instead of a WebSocket close
        /// handshake, simulating a crashed client / dropped network path. websocket-sharp's client
        /// WebSocket does not expose a way to do this itself, so this reaches into its private
        /// TcpClient via reflection.
        /// </summary>
        private static void KillConnectionAbruptly(WebSocket client)
        {
            var tcpClientField = typeof(WebSocket).GetField("_tcpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tcpClient = (TcpClient)tcpClientField.GetValue(client);
            Assert.That(tcpClient, Is.Not.Null, "test relies on websocket-sharp's private _tcpClient field");

            tcpClient.LingerState = new LingerOption(true, 0); // abortive close -> sends RST instead of FIN
            tcpClient.Client.Close(0);
        }
    }
}
