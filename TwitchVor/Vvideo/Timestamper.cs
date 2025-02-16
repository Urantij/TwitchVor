using Microsoft.Extensions.Logging;
using TwitchVor.Twitch.Checker;
using TwitchVor.Vvideo.Timestamps;

namespace TwitchVor.Vvideo;

/// <summary>
/// Под видеороликом нужно писать, когда меняется категория/тайтл
/// Мб ещё что-нибудь в будущем
/// </summary>
internal class Timestamper
{
    private readonly ILogger _logger;

    public readonly List<BaseTimestamp> timestamps = new();

    private HelixCheck? lastHelixCheck;

    public Timestamper(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    public void AddTimestamp(BaseTimestamp timestamp)
    {
        lock (timestamps)
        {
            timestamps.Add(timestamp);
        }

        _logger.LogInformation("Добавлен таймстамп \"{content}\"", timestamp.ToString());
    }

    public void HelixChecker_ChannelChecked(object? sender, TwitchCheckInfo twitchCheck)
    {
        // Если не хеликс чекс, то он должен быть всегда !онлайн, ну да ладно
        if (twitchCheck is not HelixCheck helixCheck || !twitchCheck.online)
        {
            if (lastHelixCheck == null)
                return;

            AddTimestamp(new OfflineTimestamp(twitchCheck.checkTime));

            lastHelixCheck = null;
            return;
        }

        if (lastHelixCheck?.online != true || lastHelixCheck.info.title != helixCheck.info.title ||
            lastHelixCheck.info.gameId != helixCheck.info.gameId)
        {
            AddTimestamp(new GameTimestamp(helixCheck.info.title, helixCheck.info.gameName, helixCheck.info.gameId,
                helixCheck.checkTime));
        }

        lastHelixCheck = helixCheck;
    }
}