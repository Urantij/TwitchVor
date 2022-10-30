using TwitchVor.Vvideo;

namespace TwitchVor.Finisher
{
    class VideoSummary
    {
        public readonly VideoWriter writer;
        public string? videoId;

        public bool uploaded = false;

        public TimeSpan? conversionTime;
        public TimeSpan? uploadTime;

        public VideoSummary(VideoWriter video)
        {
            this.writer = video;
        }
    }
}