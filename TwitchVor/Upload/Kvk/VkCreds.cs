using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Upload.Kvk
{
    public class VkCreds
    {
        [JsonProperty(Required = Required.Always)]
        public string ApiToken { get; set; } = "";

        [JsonProperty(Required = Required.Always)]
        public ulong ApplicationId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public long GroupId { get; set; }

        public VkCreds(string apiToken, ulong applicationId, long groupId)
        {
            ApiToken = apiToken;
            ApplicationId = applicationId;
            GroupId = groupId;
        }
    }
}