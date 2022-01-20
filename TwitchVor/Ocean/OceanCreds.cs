using Newtonsoft.Json;

namespace TwitchVor.Ocean
{
    class OceanCreds
    {
        [JsonProperty(Required = Required.Always)]
        public string ApiToken { get; set; } = "";
        [JsonProperty(Required = Required.Always)]
        public long DropletId { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string Region { get; set; } = "";

        [JsonProperty(Required = Required.Always)]
        public int SizeGigabytes { get; set; }

        [JsonProperty(Required = Required.Always)]
        /// <summary>
        /// А Я НЕ СДЕЛАЛ))
        /// </summary>
        public bool UseTempVideoWriter { get; set; } = false;
    }
}