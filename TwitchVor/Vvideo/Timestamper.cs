using TwitchVor.Twitch.Checker;

namespace TwitchVor.Vvideo
{
    class Timestamp
    {
        public readonly string content;
        /// <summary>
        /// Абсолютный. UTC пожалуйста.
        /// </summary>
        public readonly DateTime timestamp;

        public Timestamp(string content, DateTime timestamp)
        {
            this.content = content;
            this.timestamp = timestamp;
        }
    }

    /// <summary>
    /// Под видеороликом нужно писать, когда меняется категория/тайтл
    /// Мб ещё что-нибудь в будущем
    /// </summary>
    class Timestamper
    {
        readonly HelixChecker helixChecker;

        public readonly List<Timestamp> timestamps = new();
        //этого не должно быть тут но панк
        public readonly List<string> games = new();

        HelixCheck? lastHelixCheck;

        public Timestamper(HelixChecker helixChecker)
        {
            this.helixChecker = helixChecker;
            helixChecker.ChannelChecked += HelixChecker_ChannelChecked;
        }

        public void Stop()
        {
            helixChecker.ChannelChecked -= HelixChecker_ChannelChecked;
        }

        /// <summary>
        /// Время абсолютное
        /// </summary>
        /// <param name="date">ютс пожалуйста</param>
        /// <param name="content"></param>
        public void AddTimestampAbsolute(string content, DateTime date)
        {
            lock (timestamps)
            {
                timestamps.Add(new Timestamp(content, date));
            }
        }

        private void HelixChecker_ChannelChecked(object? sender, HelixCheck e)
        {
            if (lastHelixCheck != null)
            {
                if (lastHelixCheck.check.online != e.check.online)
                {
                    if (e.check.online)
                    {
                        AddTimestampAbsolute(FormOnlineStr(e.info!), e.check.checkTime);
                    }
                    else
                    {
                        AddTimestampAbsolute(FormOfflineStr(), e.check.checkTime);
                    }
                }
                else if (e.check.online && (e.info!.title != lastHelixCheck.info!.title || e.info.gameId != lastHelixCheck.info.gameId))
                {
                    AddTimestampAbsolute(FormOnlineStr(e.info), e.check.checkTime);
                }
            }
            else
            {
                if (e.check.online)
                {
                    AddTimestampAbsolute(FormOnlineStr(e.info!), e.check.checkTime);
                }
            }

            if (e.info != null)
            {
                string game = e.info.gameName ?? "???";

                if (!games.Contains(game))
                {
                    games.Add(game);
                }
            }

            lastHelixCheck = e;
        }

        private static string FormOnlineStr(TwitchChannelInfo info)
        {
            return $"{info.title} // {info.gameName ?? "???"} ({info.gameId})";
        }

        private static string FormOfflineStr()
        {
            return $"Offline";
        }
    }
}