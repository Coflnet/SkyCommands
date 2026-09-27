using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Coflnet.Sky.Commands.Services
{
    /// <summary>
    /// Periodically resolves a handful of known icons end-to-end through <see cref="PreviewService"/>
    /// (and therefore imgproxy) to catch breakage in the icon pipeline (e.g. a bad
    /// IMGPROXY_ALLOWED_SOURCES entry, an imgproxy upgrade changing default SSRF source-address
    /// behavior, or crafatar/the static mirror going down) before support tickets do.
    ///
    /// Checks three independent sources so a single-source outage (e.g. just crafatar) is
    /// distinguishable from a systemic imgproxy failure:
    ///  - "mirror": an item served directly from the static skycrypt mirror (static.coflnet.com).
    ///  - "crafatar": a player-skull head rendered by our in-cluster crafatar fork - the source
    ///    that exercises imgproxy's private-source-address allowance, since crafatar is only
    ///    reachable via a private ClusterIP.
    ///  - "vanilla": a plain Minecraft material, resolved via the vanilla-material code path.
    ///
    /// Disabled entirely when ICON_CANARY_ENABLED=false.
    /// </summary>
    public class IconCanaryService : BackgroundService
    {
        /// <summary>
        /// STRONG_DRAGON_HELMET's live texture hash (from https://api.hypixel.net/v2/resources/skyblock/items,
        /// material SKULL_ITEM) - Dragon armor has existed since 2020 and is not expected to be
        /// removed, so this is a stable default for the crafatar canary check.
        /// </summary>
        public const string DefaultSkullTextureHash = "78bc6551beb2148d939b69925140d2379fe0fab6e7d31f65df50b59e9f77a0e4";
        /// <summary>Default mirror-item tag: confirmed to be present directly on the static mirror (no fallback needed).</summary>
        public const string DefaultMirrorTag = "NECRON_HANDLE";
        /// <summary>Default vanilla-material tag: a permanent, always-craftable Skyblock item tag.</summary>
        public const string DefaultVanillaTag = "COBBLESTONE";
        public const int DefaultIntervalMinutes = 10;

        private static readonly Gauge OkGauge = Metrics.CreateGauge(
            "sky_icon_canary_ok", "Whether the icon canary could resolve the test icon for this source (1) or not (0)", "source");
        private static readonly Counter ErrorCounter = Metrics.CreateCounter(
            "sky_icon_canary_errors", "How many icon canary checks failed for this source", "source");

        private readonly IConfiguration config;
        private readonly PreviewService previewService;
        private readonly ILogger<IconCanaryService> logger;

        public IconCanaryService(IConfiguration config, PreviewService previewService, ILogger<IconCanaryService> logger)
        {
            this.config = config;
            this.previewService = previewService;
            this.logger = logger;
        }

        private bool IsEnabled => !bool.TryParse(config["ICON_CANARY_ENABLED"], out var enabled) || enabled;

        private TimeSpan Interval
        {
            get
            {
                if (!double.TryParse(config["ICON_CANARY_INTERVAL_MINUTES"], out var minutes) || minutes <= 0)
                    minutes = DefaultIntervalMinutes;
                return TimeSpan.FromMinutes(minutes);
            }
        }

        private string MirrorTag => config["ICON_CANARY_MIRROR_TAG"] ?? DefaultMirrorTag;
        private string VanillaTag => config["ICON_CANARY_VANILLA_TAG"] ?? DefaultVanillaTag;
        private string SkullTextureHash => config["ICON_CANARY_SKULL_TEXTURE_HASH"] ?? DefaultSkullTextureHash;
        private string HeadRenderBase => config["HEAD_RENDER_BASE_URL"] ?? PreviewService.DefaultHeadRenderBaseUrl;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (IsEnabled)
                {
                    try
                    {
                        await RunOnce(stoppingToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Icon canary run failed unexpectedly");
                    }
                }
                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // shutting down
                }
            }
        }

        internal async Task RunOnce(CancellationToken ct)
        {
            var previews = new Dictionary<string, PreviewService.Preview>
            {
                ["mirror"] = await SafeFetch("mirror", () => previewService.GetItemPreview(MirrorTag, false, 64)),
                ["crafatar"] = await SafeFetch("crafatar",
                    () => previewService.GetDirectPreview("crafatar", new Uri(PreviewService.BuildHeadRenderUrl(HeadRenderBase, SkullTextureHash)), 64)),
                ["vanilla"] = await SafeFetch("vanilla", () => previewService.GetItemPreview(VanillaTag, true, 64)),
            };

            foreach (var (source, ok) in Evaluate(previews))
            {
                OkGauge.WithLabels(source).Set(ok ? 1 : 0);
                if (!ok)
                {
                    ErrorCounter.WithLabels(source).Inc();
                    logger.LogError("Icon canary check failed for source {Source}", source);
                }
            }
        }

        private async Task<PreviewService.Preview> SafeFetch(string source, Func<Task<PreviewService.Preview>> fetch)
        {
            try
            {
                return await fetch();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Icon canary fetch threw for source {Source}", source);
                return null;
            }
        }

        /// <summary>
        /// Whether a single preview result counts as a healthy (ok) canary result. Pulled out as a
        /// pure function (no IO) so it's trivially unit-testable independent of the network calls.
        /// </summary>
        public static bool IsHealthy(PreviewService.Preview preview) => !IconResolver.IsMissing(preview);

        /// <summary>
        /// Maps source name -> fetched preview to source name -> healthy. Pure/no IO, see <see cref="IsHealthy"/>.
        /// </summary>
        public static IReadOnlyDictionary<string, bool> Evaluate(IReadOnlyDictionary<string, PreviewService.Preview> previewsBySource)
        {
            return previewsBySource.ToDictionary(kv => kv.Key, kv => IsHealthy(kv.Value));
        }
    }
}
