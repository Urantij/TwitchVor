namespace TwitchVor.Finisher;

public class ResultVideoSizeCache
{
    public readonly int startSegmentId;
    public readonly int endSegmentId;

    public readonly long size;

    public ResultVideoSizeCache(int startSegmentId, int endSegmentId, long size)
    {
        this.startSegmentId = startSegmentId;
        this.endSegmentId = endSegmentId;
        this.size = size;
    }
}