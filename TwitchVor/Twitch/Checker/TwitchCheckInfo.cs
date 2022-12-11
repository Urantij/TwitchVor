namespace TwitchVor.Twitch.Checker
{
    /// <summary>
    /// Информация о проверке канала
    /// </summary>
    public class TwitchCheckInfo
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