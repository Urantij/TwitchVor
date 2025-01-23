using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Chat;

class ChatCustomTimestamp : BaseTimestamp
{
    public readonly string text;
    public readonly string author;

    public ChatCustomTimestamp(string text, string author, DateTime timestamp) : base(timestamp)
    {
        this.text = text;
        this.author = author;
        this.IsUnstructuredStamp = true;
    }

    public override string MakeString()
    {
        return $"{text} ({author})";
    }
}