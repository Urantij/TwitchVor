using Newtonsoft.Json;

namespace TwitchVor.Configuration
{
    class DownloaderConfig
    {
        public string? ClientId { get; set; } = null;
        public string? OAuth { get; set; } = null;

        public SubCheckConfig? SubCheck { get; set; } = null;
    }
}