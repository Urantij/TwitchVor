using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Finisher;

namespace TwitchVor.Upload.Kvk;

public class VkVideoInfo
{
    public readonly ProcessingVideo video;
    public readonly long id;

    public VkVideoInfo(ProcessingVideo video, long id)
    {
        this.video = video;
        this.id = id;
    }
}