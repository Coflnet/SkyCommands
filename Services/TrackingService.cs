using System;
using Coflnet.Sky.Core;
using RestSharp;

namespace Coflnet.Sky.Commands
{
    public class TrackingService
    {
        /// <summary>
        /// Default Matomo site id for this backend ("SkyCommands backend" in Matomo), used unless
        /// overridden via the <c>MATOMO:SITE_ID</c> config key.
        /// </summary>
        public const int DefaultSiteId = 3;

        /// <summary>
        /// Default Matomo tracker base url, used unless overridden via the <c>MATOMO:BASE_URL</c>
        /// config key.
        /// </summary>
        public const string DefaultBaseUrl = "https://track.coflnet.com";

        readonly RestClient trackClient;
        readonly int siteId;

        public static TrackingService Instance { get; protected set; }

        static TrackingService()
        {
            Instance = new TrackingService();
        }

        public TrackingService()
            : this(
                SimplerConfig.Config.Instance["MATOMO:BASE_URL"] ?? DefaultBaseUrl,
                ParseSiteId(SimplerConfig.Config.Instance["MATOMO:SITE_ID"]))
        {
        }

        /// <summary>
        /// Allows the tracker base url and site id to be supplied directly, primarily for testing
        /// (and any future DI use) without going through the static <see cref="SimplerConfig"/> lookup.
        /// </summary>
        public TrackingService(string baseUrl, int siteId)
        {
            trackClient = new RestClient(baseUrl);
            this.siteId = siteId;
        }

        private static int ParseSiteId(string configuredSiteId)
        {
            return int.TryParse(configuredSiteId, out var id) ? id : DefaultSiteId;
        }

        /// <summary>
        /// Builds the base matomo tracking request for <c>/matomo.php</c> with the configured
        /// <c>idsite</c> and <c>rec=1</c> already set as query parameters. Every tracking request is
        /// built from this helper so the site id is defined in exactly one place.
        /// </summary>
        internal RestRequest CreateTrackRequest()
        {
            return new RestRequest("/matomo.php")
                    .AddQueryParameter("idsite", siteId.ToString())
                    .AddQueryParameter("rec", "1");
        }

        public void TrackSearch(MessageData data, string value, int resultCount, TimeSpan time)
        {
            if (value.Length <= 2)
                return;
            var genMs = ((int)time.TotalMilliseconds).ToString();
            trackClient.ExecuteAsync(BuildTrackSearchResultRequest(value, resultCount, GetUserAgent(data), genMs));
            trackClient.ExecuteAsync(BuildTrackSearchActionRequest(value, genMs));
        }

        internal RestRequest BuildTrackSearchResultRequest(string value, int resultCount, string userAgent, string genMs)
        {
            return CreateTrackRequest()
                    .AddQueryParameter("action_name", "search")
                    .AddQueryParameter("search", value)
                    .AddQueryParameter("search_count", resultCount.ToString())
                    .AddQueryParameter("ua", userAgent)
                    .AddQueryParameter("pf_srv", genMs);
        }

        internal RestRequest BuildTrackSearchActionRequest(string value, string genMs)
        {
            return CreateTrackRequest()
                    .AddQueryParameter("action_name", "search")
                    .AddQueryParameter("ua", "search")
                    .AddQueryParameter("action_name", "search/" + value)
                    .AddQueryParameter("url", "http://s/search/" + value)
                    .AddQueryParameter("cid", "1234567890abcdef")
                    .AddQueryParameter("pf_srv", genMs);
        }

        public void TrackPage(string url, string title, MessageData data)
        {
            string userAgent = GetUserAgent(data);
            TrackPage(url, title, null, userAgent);
        }

        private static string GetUserAgent(MessageData data)
        {
            var userAgent = "";
            if (data is HttpMessageData httpData)
            {
                if (httpData.context is WebsocketRequestContext context)
                {
                    userAgent = context.UserAgent;
                }
            }
            if (data is SocketMessageData socketData)
            {
                userAgent = socketData.Connection.Headers["User-Agent"];
            }

            return userAgent;
        }

        public void TrackPage(string url, string title, string referer, string userAgend = null, TimeSpan genTime = default(TimeSpan))
        {
            var request = BuildTrackPageRequest(url, title, referer, userAgend, genTime);
            trackClient.ExecuteAsync(request);
        }

        internal RestRequest BuildTrackPageRequest(string url, string title, string referer, string userAgend, TimeSpan genTime)
        {
            var request = CreateTrackRequest()
                    .AddQueryParameter("action_name", title)
                    .AddQueryParameter("url", url)
                    .AddQueryParameter("urlref", referer)
                    .AddQueryParameter("ua", userAgend);
            if (referer != null && url.Substring(0, 10) != referer.Substring(0, 10))
            {
                request.AddQueryParameter("new_visit", "1");
            }
            if (genTime != default(TimeSpan))
                request.AddQueryParameter("pf_srv", ((int)genTime.TotalMilliseconds).ToString());
            return request;
        }

        /// <summary>
        /// Tracks errors
        /// </summary>
        /// <param name="type"></param>
        internal void CommandError(string type)
        {
            var request = BuildCommandErrorRequest(type);
            trackClient.ExecuteAsync(request);
        }

        internal RestRequest BuildCommandErrorRequest(string type)
        {
            return CreateTrackRequest()
                    .AddQueryParameter("action_name", "error/" + type);
        }
    }
}