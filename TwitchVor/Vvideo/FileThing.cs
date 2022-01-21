namespace TwitchVor.Vvideo
{
    /// <summary>
    /// Хранит информацию о файле стрима на диске. Точнее о конкретном видеворолике
    /// </summary>
    class FileThing
    {
        /// <summary>
        /// Локальный путь
        /// </summary>
        public string FilePath { get; private set; }

        public string FileName { get; private set; }

        /// <summary>
        /// Полное качество
        /// </summary>
        public readonly string quality;

        /// <summary>
        /// Сколько секунд видео хранится.
        /// То есть сколько секунд видеоролика.
        /// Длительность
        /// </summary>
        public float estimatedDuration;

        /// <summary>
        /// Предполагаемый размер в байтах
        /// </summary>
        public long estimatedSize;

        /// <summary>
        /// UTC
        /// </summary>
        public DateTime? firstSegmentDate;
        /// <summary>
        /// UTC
        /// </summary>
        public DateTime? lastSegmentEndDate;

        public FileThing(string filePath, string fileName, string quality)
        {
            this.FilePath = filePath;
            this.FileName = fileName;
            this.quality = quality;

            estimatedDuration = 0;
            estimatedSize = 0;
        }

        public void SetPath(string filePath)
        {
            this.FilePath = filePath;
        }

        public void SetName(string fileName)
        {
            this.FileName = fileName;
        }

        public static string AddTempPrefix(string name)
        {
            return "temp_" + name;
        }

        public static string RemoveTempPrefix(string name)
        {
            return name["temp_".Length..];
        }
    }
}