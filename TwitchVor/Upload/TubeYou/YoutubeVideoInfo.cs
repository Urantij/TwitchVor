using TwitchVor.Finisher;

namespace TwitchVor.Upload.TubeYou;

public class YoutubeVideoInfo
{
    public readonly ProcessingVideo processingVideo;
    public readonly string videoId;

    public YoutubeVideoInfo(ProcessingVideo processingVideo, string videoId)
    {
        this.processingVideo = processingVideo;
        this.videoId = videoId;
    }
}