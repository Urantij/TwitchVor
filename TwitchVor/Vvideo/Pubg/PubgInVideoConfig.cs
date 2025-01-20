namespace TwitchVor.Vvideo.Pubg;

public class PubgInVideoConfig(string apiKey, string accountId)
{
    public string ApiKey { get; set; } = apiKey;
    public string AccountId { get; set; } = accountId;
}