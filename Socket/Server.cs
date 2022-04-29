using System;
using System.Text;
using System.Threading;
using RestSharp;
using WebSocketSharp.Server;
using WebSocketSharp;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using RateLimiter;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using System.Diagnostics;
using OpenTracing.Propagation;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Commands
{
    public class Server
    {

        public Server()
        {
            Limiter = new IpRateLimiter(ip =>
            {
                var constraint = new CountByIntervalAwaitableConstraint(10, TimeSpan.FromSeconds(1));
                var constraint2 = new CountByIntervalAwaitableConstraint(35, TimeSpan.FromSeconds(10));

                // Compose the two constraints
                return TimeLimiter.Compose(constraint, constraint2);
            });
        }
        HttpServer server;


        Prometheus.Counter requestErrors = Prometheus.Metrics.CreateCounter("requestErrors", "How often an error occured");

        private IpRateLimiter Limiter;

        /// <summary>
        /// Starts the backend server
        /// </summary>
        public async Task Start(short port = 8008, string urlPath = "/skyblock")
        {
            server = new HttpServer(port);

            server.AddWebSocketService<SkyblockBackEnd>(urlPath);
            //server.AddWebSocketService<MinecraftSocket>("/modsocket");
            // do NOT timeout after 60 sec
            server.KeepClean = false;
            server.OnOptions += (sender, e) =>
            {
                e.Response.AppendHeader("Allow", "OPTIONS, GET, POST");
                e.Response.AppendHeader("access-control-allow-origin", "*");
                e.Response.AppendHeader("Access-Control-Allow-Headers", "*");
                return Task.CompletedTask;
            };
            var getRequests = Metrics
                    .CreateCounter("total_get_requests", "Number of processed http GET requests");
            server.OnGet += async (sender, e) =>
            {
                getRequests.Inc();
                var getEvent = e as HttpRequestEventArgs;
                e.Response.AppendHeader("Allow", "OPTIONS, GET");
                e.Response.AppendHeader("access-control-allow-origin", "*");
                e.Response.AppendHeader("Access-Control-Allow-Headers", "*");
                try
                {
                    try
                    {
                        var tracer = OpenTracing.Util.GlobalTracer.Instance;
                        var builder = tracer.BuildSpan("GET:" + e.Request.RawUrl.Trim('/').Split('/', '?').FirstOrDefault())
                                    .WithTag("route", e.Request.RawUrl)
                                    .AsChildOf(tracer.Extract(BuiltinFormats.HttpHeaders, new HeaderMap(e.Request.Headers)));

                        using (var scope = builder.StartActive(true))
                        {
                            var span = scope.Span;
                            await AnswerGetRequest(new WebsocketRequestContext(e, span));
                        }

                    }
                    catch (CoflnetException ex)
                    {
                        getEvent.Response.StatusCode = 500;
                        getEvent.Response.SendChunked = true;
                        getEvent.Response.WriteContent(Encoding.UTF8.GetBytes(ex.Message));
                        return;
                    }

                }
                catch (Exception ex)
                {
                    dev.Logger.Instance.Error(ex, $"Ran into an error on get `{e.Request.RawUrl}`");
                    return;
                }

            };

            server.OnPost += async (sender, e) =>
            {


                //if (e.Request.RawUrl.StartsWith("/command/"))
                //    await HandleCommand(e.Request, e.Response);

            };
            server.Log.Output = (a, b) =>
            {
                Console.Write("socket error " + a.Message + b);
            };
            server.Start();
            Console.WriteLine("started http");
            //Console.ReadKey (true);
            await Task.Delay(Timeout.Infinite);
            server.Stop();
        }



        private static RestClient aspNet;
        private static string ProdFrontend;
        private static string StagingFrontend;

        static Server()
        {
            try
            {
                aspNet = new RestClient("http://" + SimplerConfig.Config.Instance["API_HOST"]);
                ProdFrontend = SimplerConfig.Config.Instance["FRONTEND_PROD"];
                StagingFrontend = SimplerConfig.Config.Instance["FRONTEND_STAGING"];

                CoreServer.Instance = new CommandCoreServer();
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "instantiating Server");
            }
        }

        public class CommandCoreServer : CoreServer
        {
            public override Task<TRes> ExecuteCommandWithCacheInternal<TReq, TRes>(string command, TReq reqdata)
            {
                return Server.ExecuteCommandWithCache<TReq, TRes>(command, reqdata);
            }
        }

        public async Task AnswerGetRequest(RequestContext context)
        {
            var path = context.path.Split('?')[0];


            if (path == "/stats" || path.EndsWith("/status") || path.Contains("show-status"))
            {
                await PrintStatus(context);
                Console.WriteLine(DateTime.Now);
                return;
            }

            if (path.StartsWith("/command/"))
            {
                await HandleCommand(context);
                return;
            }

            if (path == "/low")
            {
                //var relevant = Updater.LastAuctionCount.Where(a => a.Value > 0 && a.Value < 72);
                //await context.WriteAsync(JSON.Stringify(relevant));
                return;
            }

            if (context.HostName.StartsWith("skyblock") && !Program.LightClient)
            {
                context.Redirect("https://sky.coflnet.com" + path);
                return;
            }

            context.SetContentType("text/html");
            if (path == "/" || path.IsNullOrEmpty())
            {
                path = "index.html";
            }

            if (path == "/players")
            {
                await PrintPlayers(context);
                return;
            }
            if (path == "/items")
            {
                await PrintItems(context);
                return;
            }

            if (path == "/api/items/bazaar")
            {
                await PrintBazaarItems(context);
                return;
            }
            if (path == "/api/items/search")
            {
                await SearchItems(context);
                return;
            }
            if (path.StartsWith("/api") || path.StartsWith("/swagger"))
            {
                if (path.StartsWith("/swagger-"))
                    path = "/api" + path;
                // proxy to asp.net core (its better for apis)
                var result = await aspNet.ExecuteAsync(new RestRequest(path));
                context.SetContentType(result.ContentType);
                context.SetStatusCode((int)result.StatusCode);
                context.WriteAsync(result.RawBytes);
                return;
            }



            byte[] contents;
            if (path.StartsWith("/static/icon"))
            {
                await IconResolver.Instance.Resolve(context, path);
                return;
            }


            var frontendUrl = ProdFrontend;
            if (context.HostName.Contains("-"))
                frontendUrl = StagingFrontend;

            var filePath = path;
            try
            {
                if (!path.Contains("."))
                    filePath = "index.html";
                contents = new System.Net.WebClient().DownloadData($"http://{frontendUrl}/{filePath.TrimStart('/')}");

            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "loading frontend " + $"http://{frontendUrl}/{filePath.TrimStart('/')}");
                context.SetStatusCode(404);
                await context.WriteAsync("File not found, maybe you fogot to upload the fronted");
                return;
            }

            //context.ContentEncoding = Encoding.UTF8;
            if (filePath == "index.html" && !filePath.EndsWith(".js") && !filePath.EndsWith(".css"))
            {
                throw new Exception("site generation is now handled by the frontent");
            }



            if (path.EndsWith(".png") || path.StartsWith("/static/skin"))
            {
                context.SetContentType("image/png");
            }
            else if (path.EndsWith(".css"))
            {
                context.SetContentType("text/css");
            }
            else if (path.EndsWith(".svg"))
            {
                context.SetContentType("image/svg+xml");
            }
            if (path.EndsWith(".js") || path.StartsWith("/static/js"))
            {
                context.SetContentType("text/javascript");
            }
            if (path == "index.html")
            {
                context.AddHeader("cache-control", "private");
                context.SetStatusCode(404);
                await context.WriteAsync("/* This file was not found. Retry in a few miniutes :) */");
                return;
            }

            context.AddHeader("cache-control", "public,max-age=" + (3600 * 24 * 30));

            context.WriteAsync(contents);
        }

        private static void TrackGeneration(string path, Stopwatch watch, WebsocketRequestContext httpContext)
        {
            if (path.StartsWith("/static"))
                TrackingService.Instance.TrackPage(httpContext.original.Request?.Url?.ToString(),
                                            "",
                                            null,
                                            null,
                                            watch.Elapsed);
            else
                TrackingService.Instance.TrackPage(httpContext.original.Request?.Url?.ToString(),
                        "",
                        httpContext.original.Request?.UrlReferrer?.ToString(),
                        httpContext.original.Request?.UserAgent,
                        watch.Elapsed);
        }

        private Task HandleApiRequest(RequestContext context)
        {
            throw new NotImplementedException();
        }

        private async Task HandleCommand(RequestContext context)
        {
            HttpMessageData data = new HttpMessageData(context);
            try
            {
                /*var conId = req.Headers["ConId"];
                if (conId == null || conId.Length < 32)
                    throw new CoflnetException("invalid_conid", "The 'ConId' Header has to be at least 32 characters long and generated randomly");
                conId = conId.Truncate(32);
                data.SetUserId = id =>
                {
                    this.ConnectionToUserId.TryAdd(conId, id);
                };

                if (ConnectionToUserId.TryGetValue(conId, out int userId))
                    data.UserId = userId;

                if (data.Type == "test")
                {
                    Console.WriteLine(req.RemoteEndPoint.Address.ToString());
                    foreach (var item in req.Headers.AllKeys)
                    {
                        Console.WriteLine($"{item.ToString()}: {req.Headers[item]}");
                    }
                    return;
                } */

                //if ((await CacheService.Instance.TryFromCacheAsync(data)).IsFlagSet(CacheStatus.VALID))
                //    return;

                /*  var ip = req.Headers["Cf-Connecting-Ip"];
                  if(ip == null)
                      ip = req.Headers["X-Real-Ip"];
                  if(ip == null)
                      ip = req.RemoteEndPoint.Address.ToString();
                  Console.WriteLine($"rc {data.Type} {data.Data.Truncate(20)}");
                  await Limiter.WaitUntilAllowed(ip); */
                //ExecuteCommandWithCache

                if (SkyblockBackEnd.Commands.TryGetValue(data.Type, out Command command))
                {
                    try
                    {
                        await ExecuteWithCacheInternal(data);

                        // TODO make this work again
                        if (!data.CompletionSource.Task.Wait(TimeSpan.FromSeconds(30)))
                        {
                            throw new CoflnetException("timeout", "could not generate a response, please report this and try again");
                        }
                        return;
                    }
                    catch (CoflnetException ex)
                    {
                        context.SetStatusCode(400);
                        await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
                    }
                    catch (Exception e)
                    {
                        if (e.InnerException is CoflnetException ex)
                        {
                            context.SetStatusCode(400);
                            await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));

                        }
                        else
                        {
                            Console.WriteLine("holly shit");
                            requestErrors.Inc();
                            data.CompletionSource.TrySetException(e);
                            dev.Logger.Instance.Error(e);
                            throw e;
                        }
                    }
                }
                else
                    throw new CoflnetException("unkown_command", "Command not known, check the docs");
            }
            catch (CoflnetException ex)
            {
                context.SetStatusCode(400);
                await context.WriteAsync(JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
            }
            catch (Exception ex)
            {
                requestErrors.Inc();
                context.SetStatusCode(500);

                using var span = OpenTracing.Util.GlobalTracer.Instance.BuildSpan("error").WithTag("error",true).StartActive();
                span.Span.Log(ex.ToString());
                var traceId = System.Net.Dns.GetHostName().Replace("commands", "").Trim('-') + "." + span.Span.Context.TraceId;
                await data.SendBack(new MessageData("error", JsonConvert.SerializeObject(new { Slug = "error", Message = "An unexpected internal error occured, make sure the format of Data is correct", traceId })));
                TrackingService.Instance.CommandError(data.Type);
                dev.Logger.Instance.Error(ex, "Fatal error on Command");
            }
        }

        public static void ExecuteCommandHeadless(MessageData data)
        {
            if (!SkyblockBackEnd.Commands.TryGetValue(data.Type, out Command command))
                return; // unkown command

            try
            {
                command.Execute(data);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Failed to update cache");
            }
        }

        /// <summary>
        /// keeps track of in flight cache commands
        /// </summary>
        private static ConcurrentDictionary<string, SemaphoreSlim> RecentlyStartedCommands = new ConcurrentDictionary<string, SemaphoreSlim>();


        public static async Task<TRes> ExecuteCommandWithCache<TReq, TRes>(string command, TReq reqdata)
        {
            var source = new TaskCompletionSource<TRes>();
            var data = new ProxyMessageData<TReq, TRes>(command, reqdata, source);

            await ExecuteWithCacheInternal(data);
            return await source.Task;

        }

        private static async Task ExecuteWithCacheInternal(MessageData data)
        {
            var key = data.Type + data.Data;
            if (!(await CacheService.Instance.TryFromCacheAsync(data)).IsFlagSet(CacheStatus.VALID))
            {
                try
                {
                    // wait a bit for the same response
                    if (RecentlyStartedCommands.TryGetValue(key, out SemaphoreSlim value))
                    {
                        await value.WaitAsync(200 + new Random().Next(300));
                        OpenTracing.Util.GlobalTracer.Instance.ActiveSpan?.SetTag("cache", "waited");
                        // if it is available now, return it
                        if ((await CacheService.Instance.TryFromCacheAsync(data)).IsFlagSet(CacheStatus.VALID))
                        {
                            OpenTracing.Util.GlobalTracer.Instance.ActiveSpan?.Log("hit after wait");
                            return;
                        }
                    }
                    RecentlyStartedCommands[key] = new SemaphoreSlim(1, 1);

                    OpenTracing.Util.GlobalTracer.Instance.ActiveSpan?.Log("miss");
                    await SkyblockBackEnd.Commands[data.Type].Execute(data);
                }
                finally
                {
                    if (RecentlyStartedCommands.TryRemove(key, out SemaphoreSlim startTime) && startTime.CurrentCount == 0)
                        startTime.Release();
                }
            }
            else
            {
                OpenTracing.Util.GlobalTracer.Instance.ActiveSpan?.SetTag("cache", "hit");
            }
        }

        private static async Task PrintStatus(RequestContext res)
        {
            var data = new Stats()
            {
                NameRequests = Program.RequestsSinceStart,
                CacheSize = CacheService.Instance.CacheSize,
                ConnectionCount = SkyblockBackEnd.ConnectionCount
            };

            // determine status
            res.SetStatusCode(200);
            var maxTime = DateTime.Now.Subtract(new TimeSpan(0, 5, 0));

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            await res.WriteAsync(json);
        }

        private static async Task PrintBazaarItems(RequestContext context)
        {
            var data = await ItemDetails.Instance.GetBazaarItems();


            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data.Select(i => new { i.Name, i.Tag, i.MinecraftType, i.IconUrl }));
            await context.WriteAsync(json);
        }

        private static async Task PrintPlayers(RequestContext reqcon)
        {
            using (var context = new HypixelContext())
            {
                var data = context.Players.OrderByDescending(p => p.UpdatedAt).Select(p => new { p.Name, p.UuId }).Take(10000).AsParallel();
                StringBuilder builder = GetSiteBuilder("Player");
                foreach (var item in data)
                {
                    if (item.Name == null)
                        continue;
                    builder.AppendFormat("<li><a href=\"{0}\">{1}</a></li>", $"/player/{item.UuId}/{item.Name}", $"{item.Name} auctions");
                }
                await reqcon.WriteAsync(builder.ToString());
            }
        }

        private static async Task PrintItems(RequestContext reqcon)
        {
            using (var context = new HypixelContext())
            {
                var data = await context.Items.Select(p => new { p.Tag }).Take(10000).ToListAsync();
                StringBuilder builder = GetSiteBuilder("Item");
                foreach (var item in data)
                {
                    var name = ItemDetails.TagToName(item.Tag);
                    builder.AppendFormat("<li><a href=\"{0}\">{1}</a></li>", $"/item/{item.Tag}/{name}", $"{name} auctions");
                }
                await reqcon.WriteAsync(builder.ToString());
            }
        }

        private static StringBuilder GetSiteBuilder(string topic)
        {
            var builder = new StringBuilder(20000);
            builder.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
            builder.Append($"<link rel=\"icon\" href=\"/favicon.ico\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"/><title>{topic} List</title>");
            builder.Append("<style>li {padding:10px;}</style></head><body>");
            builder.Append($"<h2>List of the most recently updated {topic}s</h2><a href=\"https://sky.coflnet.com\">back to the start page</a><ul>");
            return builder;
        }

        private static async Task SearchItems(RequestContext context)
        {
            var term = context.QueryString["term"];
            Console.WriteLine("searchig for:");
            Console.WriteLine(term);
            var data = await ItemDetails.Instance.Search(term);


            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            await context.WriteAsync(json);
        }

        public void Stop()
        {
            server.Stop();
        }
    }
}
