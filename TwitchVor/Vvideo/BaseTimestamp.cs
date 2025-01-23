namespace TwitchVor.Vvideo
{
    public abstract class BaseTimestamp
    {
        /// <summary>
        /// Абсолютный. UTC пожалуйста.
        /// </summary>
        public readonly DateTime timestamp;

        public TimeSpan? Offset { get; protected set; }

        /// <summary>
        /// Если вне структуры, то пишется в описании видео отдельно после структурных стампов.
        /// </summary>
        public bool IsUnstructuredStamp { get; protected set; }

        protected BaseTimestamp(DateTime timestamp)
        {
            this.timestamp = timestamp;
        }

        public DateTime GetTimeWithOffset()
        {
            if (Offset == null)
                return timestamp;

            return timestamp + Offset.Value;
        }

        public void SetOffset(TimeSpan offset)
        {
            Offset = offset;
        }

        /// <summary>
        /// Создать строку, которая будет в описании.
        /// </summary>
        /// <returns></returns>
        public abstract string MakeString();
    }
}