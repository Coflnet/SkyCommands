using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.Services;
using System.Threading.Channels;
using Coflnet.Sky.Core;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.Commands
{
    public class FullSearchCommand : Command
    {
        private const string Type = "searchResponse";

        public override async Task Execute(MessageData data)
        {
            List<SearchResultItem> orderedResult = await NewMethod(data).ConfigureAwait(false);
            var maxAge = A_DAY;
            if (orderedResult.Count() == 0)
                maxAge = 0;
            await data.SendBack(data.Create(Type, orderedResult, maxAge));
        }

        private async Task<List<SearchResultItem>> NewMethod(MessageData data)
        {
            var watch = Stopwatch.StartNew();
            var search = ItemSearchCommand.RemoveInvalidChars(data.Data);
            var cancelationSource = new CancellationTokenSource();
            cancelationSource.CancelAfter(5000);
            var results = await SearchService.Instance.Search(search, cancelationSource.Token);

            var result = new ConcurrentBag<SearchResultItem>();
            var pullTask = Task.Run(async () =>
            {
                while (!cancelationSource.IsCancellationRequested)
                {
                    var r = await results.Reader.ReadAsync();
                        result.Add(r);
                        if (result.Count > 15)
                            return; // return early

                        var lastTask = Task.Run(() => LoadPreview(watch, r), cancelationSource.Token).ConfigureAwait(false);
                    
                }

            }, cancelationSource.Token);

            data.Log($"Waiting half a second " + watch.Elapsed);
            await Task.WhenAny(pullTask, Task.Delay(TimeSpan.FromMilliseconds(320)));
            DequeueResult(results, result);
            if (result.Count == 0)
            {
                await Task.WhenAny(pullTask, Task.Delay(TimeSpan.FromMilliseconds(600)));
                DequeueResult(results, result);
            }

            cancelationSource.Cancel();
            DequeueResult(results, result);
            data.Log($"Started sorting {search} " + watch.Elapsed);
            List<SearchResultItem> orderedResult = SearchService.Instance.RankSearchResults(search, result);
            data.Log($"making response {watch.Elapsed} total: {System.DateTime.Now - data.Created}");
            var elapsed = watch.Elapsed;
            var trackTask = Task.Run(() =>
            {
                if (!(data is ProxyMessageData<string, object>))
                    TrackingService.Instance.TrackSearch(data, data.Data, orderedResult.Count, elapsed);
            }).ConfigureAwait(false);
            return orderedResult;
        }

        private static void DequeueResult(Channel<SearchResultItem> results, ConcurrentBag<SearchResultItem> result)
        {
            while (results.Reader.TryRead(out SearchResultItem r))
                result.Add(r);
        }

        private async Task LoadPreview(Stopwatch watch, SearchResultItem r)
        {
            try
            {
                PreviewService.Preview preview = null;
                if (r.Type == "player")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("pPrev", r.Id);
                else if (r.Type == "item")
                    preview = await Server.ExecuteCommandWithCache<string, PreviewService.Preview>("iPrev", r.Id.Split('?').First());

                if (preview == null)
                    return;

                r.Image = preview.Image;
                r.IconUrl = preview.ImageUrl;
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Failed to load preview for " + r.Id);
            }
        }
    }
}