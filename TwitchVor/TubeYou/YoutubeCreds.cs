using Newtonsoft.Json;

namespace TwitchVor.TubeYou
{
    public class YoutubeCreds
    {
        [JsonProperty(Required = Required.Always)]
        public string RefreshToken { get; private set; } = "";

        [JsonProperty(Required = Required.Always)]
        public string ClientId { get; private set; } = "";
        [JsonProperty(Required = Required.Always)]
        public string ClientSecret { get; private set; } = "";

        [JsonProperty(Required = Required.Always)]
        public string[] VideoTags { get; private set; } = Array.Empty<string>();
    }
}