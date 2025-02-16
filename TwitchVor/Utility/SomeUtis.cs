using TwitchStreamDownloader.Download;

namespace TwitchVor.Utility;

public static class SomeUtis
{
    // https://stackoverflow.com/a/4975942/21555531
    private static readonly string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB

    public static string MakeSizeFormat(long size)
    {
        if (size == 0)
            return "0" + suf[0];

        long bytes = Math.Abs(size);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);

        return (Math.Sign(size) * num).ToString() + suf[place];
    }

    public static string MakeFormat(Quality quality)
    {
        if (quality.resolution == null)
            return $"{quality.fps}";

        return $"{quality.resolution.width}x{quality.resolution.height}:{quality.fps}";
    }
}