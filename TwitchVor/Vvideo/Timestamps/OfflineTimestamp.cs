namespace TwitchVor.Vvideo.Timestamps;

internal class OfflineTimestamp : BaseTimestamp
{
    public OfflineTimestamp(DateTime timestamp) : base(timestamp)
    {
    }

    public override string MakeString()
    {
        return "Offline";
    }
}