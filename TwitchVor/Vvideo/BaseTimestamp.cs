namespace TwitchVor.Vvideo
{
    public abstract class BaseTimestamp
    {
        /// <summary>
        /// Абсолютный. UTC пожалуйста.
        /// </summary>
        public readonly DateTime timestamp;

        /// <summary>
        /// Если фейк, то нужно писать в описании видео так, чтобы отметки на видео не было.
        /// </summary>
        public bool IsFakeStamp { get; protected set; }

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