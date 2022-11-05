using Newtonsoft.Json;

namespace TwitchVor.Space.OceanDigital
{
    class OceanCreds
    {
        [JsonProperty(Required = Required.Always)]
        public string ApiToken { get; set; } = "";
        [JsonProperty(Required = Required.Always)]
        public long DropletId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int SizeGigabytes { get; set; }

        //В рантайме достану по дроплету
        [JsonIgnore]
        public string Region { get; set; } = "";
    }
}