using Newtonsoft.Json;
using TwitchVor.Ocean;
using TwitchVor.TubeYou;

namespace TwitchVor.Configuration
{
    class Config
    {
        [JsonProperty(Required = Required.AllowNull)]
        public string? Channel { get; private set; } = null;
        [JsonProperty(Required = Required.AllowNull)]
        public string? ChannelId { get; set; } = null;

        [JsonProperty(Required = Required.AllowNull)]
        public string? TwitchAPIClientId { get; private set; } = null;
        [JsonProperty(Required = Required.AllowNull)]
        public string? TwitchAPISecret { get; private set; } = null;

        public string PreferedVideoQuality { get; private set; } = "720p";
        public string PreferedVideoFps { get; private set; } = "p60";

        [JsonProperty(Required = Required.AllowNull)]
        public YoutubeCreds? YouTube { get; private set; } = null;

        [JsonProperty(Required = Required.AllowNull)]
        public OceanCreds? Ocean { get; private set; } = null;

        //Checker

        /// <summary>
        /// Как часто хеликс проверяет стрим
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public TimeSpan HelixCheckDelay { get; private set; } = TimeSpan.FromSeconds(25);

        [JsonProperty(Required = Required.Always)]
        public TimeSpan PubsubReconnectDelay { get; private set; } = TimeSpan.FromSeconds(10);

        //Stream

        public string? DownloaderClientId { get; private set; } = null;
        public string? DownloaderOAuth { get; private set; } = null;

        /// <summary>
        /// Как долго считать офнутый стрим не офнутым
        /// </summary>
        public TimeSpan StreamContinuationCheckTime { get; private set; } = TimeSpan.FromSeconds(22); //секунд 22
        /// <summary>
        /// Как долго ждать переподруба
        /// </summary>
        public TimeSpan StreamRestartCheckTime { get; private set; } = TimeSpan.FromHours(1); //часик

        public TimeSpan SegmentAccessReupdateDelay { get; private set; } = TimeSpan.FromMinutes(1);

        public TimeSpan SegmentDownloaderTimeout { get; private set; } = TimeSpan.FromSeconds(5);

        //File

        public string VideosDirectoryName { get; private set; } = "Videos";

        /// <summary>
        /// Байты
        /// </summary>
        public long MaximumVideoSize { get; private set; } = 1000L * 1000L * 1000L * 100L; // ~100 гигов
        public TimeSpan MaximumVideoDuration { get; private set; } = TimeSpan.FromHours(12) * 0.98f; // ~12 часов

        /// <summary>
        /// Информация о длительности сегмента не всегда правдива.
        /// Поэтому какую то погрешность заложим.
        /// Но если больше, то мы потеряли контент
        /// </summary>
        public TimeSpan MinimumSegmentSkipDelay { get; private set; } = TimeSpan.FromSeconds(0.2);
        public TimeSpan FileWriteTimeout { get; private set; } = TimeSpan.FromSeconds(5);
    }
}