namespace TwitchVor.Upload.TubeYou;

public class YoutubeCreds
{
    public required string RefreshToken { get; set; }
    public required string UserId { get; set; }

    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }

    public string[]? VideoTags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Сколько ждём, прежде чем начнём дудосить ютуб
    /// </summary>
    public TimeSpan? VideoDescriptionUpdateDelay { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Как часто проверять
    /// </summary>
    public TimeSpan? VideoProcessingCheckDelay { get; set; } = TimeSpan.FromMinutes(5);

    public YoutubeCreds(string refreshToken, string userId, string clientId, string clientSecret, string[]? videoTags,
        TimeSpan? videoDescriptionUpdateDelay, TimeSpan? videoProcessingCheckDelay)
    {
        RefreshToken = refreshToken;
        UserId = userId;
        ClientId = clientId;
        ClientSecret = clientSecret;
        VideoTags = videoTags;
        VideoDescriptionUpdateDelay = videoDescriptionUpdateDelay;
        VideoProcessingCheckDelay = videoProcessingCheckDelay;
    }
}