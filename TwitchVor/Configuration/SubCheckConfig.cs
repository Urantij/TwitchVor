using Newtonsoft.Json;

namespace TwitchVor.Configuration
{
    class SubCheckConfig
    {
#nullable disable
        [JsonProperty(Required = Required.Always)]
        public string AppClientId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string AppSecret { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string UserId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string RefreshToken { get; set; }
#nullable enable

        [JsonProperty(Required = Required.Default)]
        public bool CheckSubOnStart { get; set; } = false;
    }
}