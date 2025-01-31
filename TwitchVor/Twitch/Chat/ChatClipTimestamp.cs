using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Chat;

public class ChatClipTimestamp : BaseTimestamp
{
    public string CreatorName { get; private set; }
    public string CreatorId { get; private set; }

    public string Title { get; private set; }

    public string Url { get; private set; }

    public ChatClipTimestamp(string creatorName, string creatorId, string title, string url, DateTime timestamp) :
        base(timestamp)
    {
        CreatorName = creatorName;
        CreatorId = creatorId;
        Title = title;
        Url = url;
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