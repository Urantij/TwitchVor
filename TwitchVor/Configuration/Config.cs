using Newtonsoft.Json;
using TwitchVor.Ocean;
using TwitchVor.TubeYou;

namespace TwitchVor.Configuration
{
    class Config
    {
        [JsonProperty(Required = Required.AllowNull)]
        public string? Channel { get; set; } = null;
        [JsonProperty(Required = Required.AllowNull)]
        public string? ChannelId { get; set; } = null;

        [JsonProperty(Required = Required.AllowNull)]
        public string? TwitchAPIClientId { get; set; } = null;
        [JsonProperty(Required = Required.AllowNull)]
        public string? TwitchAPISecret { get; set; } = null;

        public string PreferedVideoQuality { get; set; } = "720p";
        public string PreferedVideoFps { get; set; } = "p60";

        [JsonProperty(Required = Required.AllowNull)]
        public YoutubeCreds? YouTube { get; set; } = null;

        [JsonProperty(Required = Required.AllowNull)]
        public OceanCreds? Ocean { get; set; } = null;

        //Checker

        /// <summary>
        /// Как часто хеликс проверяет стрим
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public TimeSpan HelixCheckDelay { get; set; } = TimeSpan.FromSeconds(25);

        [JsonProperty(Required = Required.Always)]
        public TimeSpan PubsubReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);

        //Stream

        public string? DownloaderClientId { get; set; } = null;
        public string? DownloaderOAuth { get; set; } = null;

        /// <summary>
        /// Как долго считать офнутый стрим не офнутым
        /// </summary>
        public TimeSpan StreamContinuationCheckTime { get; set; } = TimeSpan.FromSeconds(22); //секунд 22
        /// <summary>
        /// Как долго ждать переподруба
        /// </summary>
        public TimeSpan StreamRestartCheckTime { get; set; } = TimeSpan.FromHours(1); //часик

        public TimeSpan SegmentAccessReupdateDelay { get; set; } = TimeSpan.FromMinutes(1);

        public TimeSpan SegmentDownloaderTimeout { get; set; } = TimeSpan.FromSeconds(5);

        //File

        public string VideosDirectoryName { get; set; } = "Videos";

        /// <summary>
        /// Байты
        /// </summary>
        public long MaximumVideoSize { get; set; } = 1000L * 1000L * 1000L * 100L; // ~100 гигов
        public TimeSpan MaximumVideoDuration { get; set; } = TimeSpan.FromHours(12) * 0.98f; // ~12 часов

        /// <summary>
        /// Информация о длительности сегмента не всегда правдива.
        /// Поэтому какую то погрешность заложим.
        /// Но если больше, то мы потеряли контент
        /// </summary>
        public TimeSpan MinimumSegmentSkipDelay { get; set; } = TimeSpan.FromSeconds(0.2);
        public TimeSpan FileWriteTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}