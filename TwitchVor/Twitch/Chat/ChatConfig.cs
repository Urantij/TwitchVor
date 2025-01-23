namespace TwitchVor.Twitch.Chat;

public class ChatConfig
{
    public string? Username { get; set; }
    public string? Token { get; set; }

    /// <summary>
    /// На какое время сдвигать стамп из чата. -30 сек норм.
    /// </summary>
    public TimeSpan TimestampOffset { get; set; } = TimeSpan.FromSeconds(-30);
}