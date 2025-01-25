using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Chat;

public class ChatClipTimestamp : BaseTimestamp
{
    public string CreatorName { get; private set; }
    public string CreatorId { get; private set; }

    public string Title { get; private set; }

    /// <summary>
    /// Ето время создания клипа... то есть не время клипа на воде...
    /// Да и на воде найти клип неряльно, потому что если стрим дробился, понять, где как и когда клип был трудно.
    /// </summary>
    public DateTime Created { get; private set; }

    /// <summary>
    /// The length of the clip, in seconds. Precision is 0.1.
    /// </summary>
    public float Duration { get; private set; }

    public string Url { get; private set; }

    public ChatClipTimestamp(string creatorName, string creatorId, string title, DateTime created, float duration,
        string url, DateTime timestamp) : base(timestamp)
    {
        CreatorName = creatorName;
        CreatorId = creatorId;
        Title = title;
        Created = created;
        Duration = duration;
        Url = url;
        IsUnstructuredStamp = true;
    }

    public override string MakeString()
    {
        return $"""
                {Title}
                {CreatorName} ({CreatorId})
                {Url}
                """;
    }
}