namespace TwitchVor.Vvideo
{
    abstract class BaseTimestamp
    {
        /// <summary>
        /// Абсолютный. UTC пожалуйста.
        /// </summary>
        public readonly DateTime timestamp;

        protected BaseTimestamp(DateTime timestamp)
        {
            this.timestamp = timestamp;
        }

        public override string ToString()
        {
            return "???";
        }
    }
}