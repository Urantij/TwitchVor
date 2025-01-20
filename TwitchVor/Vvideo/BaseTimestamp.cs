namespace TwitchVor.Vvideo
{
    public abstract class BaseTimestamp
    {
        /// <summary>
        /// Абсолютный. UTC пожалуйста.
        /// </summary>
        public readonly DateTime timestamp;

        protected BaseTimestamp(DateTime timestamp)
        {
            this.timestamp = timestamp;
        }

        /// <summary>
        /// Создать строку, которая будет в описании.
        /// </summary>
        /// <returns></returns>
        public abstract string MakeString();
    }
}