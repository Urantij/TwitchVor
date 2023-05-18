using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
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
