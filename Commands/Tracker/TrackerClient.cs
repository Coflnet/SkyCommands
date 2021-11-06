using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public static class TrackerClient
    {
        public static RestClient Client = new RestClient("http://" + SimplerConfig.Config.Instance["TRACKER_HOST"]);

    }
}