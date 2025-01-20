namespace TwitchVor.Twitch.Checker;

/// <summary>
/// Доп инфа о стриме.
/// </summary>
public class TwitchChannelInfo
{
    public readonly string title;

    /// <summary>
    /// Нулл, если не удалось найти информацию.
    /// </summary>
    public readonly string gameName;

    public readonly string gameId;
    public readonly int viewers;

    public TwitchChannelInfo(string title, string gameName, string gameId, int viewers)
    {
        this.title = title;
        this.gameName = gameName;
        this.gameId = gameId;
        this.viewers = viewers;
    }
}

/// <summary>
/// Результат проверки канала от хеликса.
/// </summary>
public class HelixCheck : TwitchCheckInfo
{
    public readonly TwitchChannelInfo info;

    public HelixCheck(bool online, DateTime checkTime, TwitchChannelInfo info)
        : base(online, checkTime)
    {
        this.info = info;
    }
}