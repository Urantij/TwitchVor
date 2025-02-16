using Newtonsoft.Json;

namespace TwitchVor.Twitch;

internal class DownloaderConfig
{
    public string? ClientId { get; set; } = null;
    public string? OAuth { get; set; } = null;

    [JsonProperty(Required = Required.Always)]
    public string UserAgent { get; set; }

    public SubCheckConfig? SubCheck { get; set; } = null;
}