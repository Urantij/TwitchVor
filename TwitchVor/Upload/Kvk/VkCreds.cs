using Newtonsoft.Json;

namespace TwitchVor.Upload.Kvk;

public class VkAppConfig
{
    [JsonProperty(Required = Required.Always)]
    public string ApiToken { get; set; } = "";

    [JsonProperty(Required = Required.Always)]
    public ulong ApplicationId { get; set; }

    public VkAppConfig(string apiToken, ulong applicationId)
    {
        ApiToken = apiToken;
        ApplicationId = applicationId;
    }
}

public class VkCreds
{
    [JsonProperty(Required = Required.Always)]
    public long GroupId { get; set; }

    [JsonProperty(Required = Required.Always)]
    public VkAppConfig Uploader { get; set; }

    [JsonProperty(Required = Required.Default)]
    public VkAppConfig? WallRunner { get; set; }

    public VkCreds(long groupId, VkAppConfig uploader, VkAppConfig? wallRunner)
    {
        GroupId = groupId;
        Uploader = uploader;
        WallRunner = wallRunner;
    }
}