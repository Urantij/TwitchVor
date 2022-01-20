namespace TwitchVor.Vvideo
{
    class SkipInfo
    {
        /// <summary>
        /// Абсолютное время. UTC.
        /// Получается конец последнего видимого сегмента
        /// </summary>
        public readonly DateTime whenStarted;
        /// <summary>
        /// Абсолютное время. UTC.
        /// Получается начало нового видимого сегмента.
        /// </summary>
        public readonly DateTime whenEnded;

        public SkipInfo(DateTime whenStarted, DateTime whenEnded)
        {
            this.whenStarted = whenStarted;
            this.whenEnded = whenEnded;
        }
    }
}