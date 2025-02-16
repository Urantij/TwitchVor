using Newtonsoft.Json;

namespace TwitchVor.Space.TimeWeb;

public class TimewebConfig
{
    [JsonProperty(Required = Required.Always)]
    public string Token { get; set; }

    [JsonProperty(Required = Required.Default)]
    public bool ValidateTokenOnStart { get; set; } = true;

    [JsonProperty(Required = Required.Default)]
    public TimeSpan UploadRequestTimeout { get; set; } = TimeSpan.FromMinutes(1);

    [JsonProperty(Required = Required.Default)]
    public TimeSpan DownloadRequestTimeout { get; set; } = TimeSpan.FromHours(2);

    [JsonProperty(Required = Required.Default)]
    public long PerFileSize { get; set; } = 100 * 1024 * 1024;

    public TimewebConfig(string token, bool validateTokenOnStart, TimeSpan uploadRequestTimeout,
        TimeSpan downloadRequestTimeout, long perFileSize)
    {
        Token = token;
        ValidateTokenOnStart = validateTokenOnStart;
        UploadRequestTimeout = uploadRequestTimeout;
        DownloadRequestTimeout = downloadRequestTimeout;
        PerFileSize = perFileSize;
    }
}