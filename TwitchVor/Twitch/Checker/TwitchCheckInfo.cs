namespace TwitchVor.Twitch.Checker
{
    /// <summary>
    /// Доп инфа о стриме
    /// </summary>
    class TwitchChannelInfo
    {
        public string title;
        /// <summary>
        /// Нулл, если что то обосралось (возможно)
        /// </summary>
        public string? gameName;
        public string gameId;
        public int viewers;

        public TwitchChannelInfo(string title, string gameId, int viewers)
        {
            this.title = title;
            this.gameId = gameId;
            this.viewers = viewers;
        }
    }

    /// <summary>
    /// Результат проверки канала от хеликса
    /// </summary>
    class HelixCheck
    {
        /// <summary>
        /// Если онлайн, не нулл
        /// </summary>
        public TwitchChannelInfo? info;
        public TwitchCheckInfo check;

        public HelixCheck(TwitchCheckInfo check)
        {
            this.check = check;
        }
    }
    
    /// <summary>
    /// Информация о проверке канала
    /// </summary>
    class TwitchCheckInfo
    {
        public readonly bool online;
        /// <summary>
        /// UTC
        /// </summary>
        public readonly DateTime checkTime;

        public TwitchCheckInfo(bool online, DateTime checkTime)
        {
            this.online = online;
            this.checkTime = checkTime;
        }
    }
}