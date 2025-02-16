using Newtonsoft.Json;

namespace TwitchVor.Conversion;

public class ConversionConfig
{
    [JsonProperty(Required = Required.Always)]
    public string FfmpegPath { get; set; } = Path.Combine("./ffmpeg", "ffmpeg");
}