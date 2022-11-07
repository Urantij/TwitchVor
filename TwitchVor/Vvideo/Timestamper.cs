using Microsoft.Extensions.Logging;
using TwitchVor.Twitch.Checker;
using TwitchVor.Vvideo.Timestamps;

namespace TwitchVor.Vvideo
{
    /// <summary>
    /// Под видеороликом нужно писать, когда меняется категория/тайтл
    /// Мб ещё что-нибудь в будущем
    /// </summary>
    class Timestamper
    {
        readonly ILogger _logger;

        public readonly List<BaseTimestamp> timestamps = new();

        HelixCheck? lastHelixCheck;

        public Timestamper(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());
        }

        private void AddTimestamp(BaseTimestamp timestamp)
        {
            lock (timestamps)
            {
                timestamps.Add(timestamp);
            }

            _logger.LogInformation("Добавлен таймстамп \"{content}\"", timestamp.ToString());
        }

        public void HelixChecker_ChannelChecked(object? sender, HelixCheck e)
        {
            if (lastHelixCheck != null)
            {
                if (lastHelixCheck.check.online != e.check.online)
                {
                    if (e.check.online)
                    {
                        //если онлайн, то всегда есть инфо
                        AddTimestamp(new GameTimestamp(e.info!.title, e.info.gameName, e.info.gameId, e.check.checkTime));
                    }
                    else
                    {
                        AddTimestamp(new OfflineTimestamp(e.check.checkTime));
                    }
                }
                else if (e.check.online && (e.info!.title != lastHelixCheck.info!.title || e.info.gameId != lastHelixCheck.info.gameId))
                {
                    AddTimestamp(new GameTimestamp(e.info.title, e.info.gameName, e.info.gameId, e.check.checkTime));
                }
            }
            else
            {
                if (e.check.online)
                {
                    //если онлайн, то всегда есть инфо
                    //TODO сделать раздельные классы для офлаин и онлайн чеков? заебало как то
                    AddTimestamp(new GameTimestamp(e.info!.title, e.info.gameName, e.info.gameId, e.check.checkTime));
                }
            }

            lastHelixCheck = e;
        }
    }
}