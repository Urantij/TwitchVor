using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchStreamDownloader.Download;

namespace TwitchVor.Utility;

public static class SomeUtis
{
    public static string MakeFormat(Quality quality)
    {
        if (quality.resolution == null)
            return $"{quality.fps}";

        return $"{quality.resolution.width}x{quality.resolution.height}:{quality.fps}";
    }
}
