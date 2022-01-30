using TwitchVor.TubeYou;
using TwitchVor.Vvideo;

namespace TwitchVor.Finisher
{
    class UploadedVideo
    {
        public readonly VideoWriter writer;
        public readonly string videoId;

        public readonly TimeSpan? processingTime;
        public readonly TimeSpan uploadingTime;

        public UploadedVideo(VideoWriter writer, string videoId, TimeSpan? processingTime, TimeSpan uploadingTime)
        {
            this.writer = writer;
            this.videoId = videoId;
            this.processingTime = processingTime;
            this.uploadingTime = uploadingTime;
        }
    }
}