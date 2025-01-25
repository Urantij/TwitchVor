namespace TwitchVor.Twitch.Chat;

public class ChatConfig
{
    public string? Username { get; set; }
    public string? Token { get; set; }

    /// <summary>
    /// На какое время сдвигать стамп из чата. -30 сек норм.
    /// </summary>
    public TimeSpan TimestampOffset { get; set; } = TimeSpan.FromSeconds(-30);

    /// <summary>
    /// При появлении в чате ссылки на клип, попытаться взять инфу с него и записать в стампы.
    /// </summary>
    public ChatClipConfig? FetchClips { get; set; } = new();
}

public class ChatClipConfig
{
    public TimeSpan ClipOffset { get; set; } = TimeSpan.FromSeconds(-30);
}