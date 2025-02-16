using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Chat;

internal class ChatCustomTimestamp : BaseTimestamp
{
    public readonly string text;
    public readonly string author;

    public ChatCustomTimestamp(string text, string author, DateTime timestamp) : base(timestamp)
    {
        this.text = text;
        this.author = author;
    }

    public override string MakeString()
    {
        return $"{text} ({author})";
    }
}