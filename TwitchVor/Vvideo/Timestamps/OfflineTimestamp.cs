namespace TwitchVor.Vvideo.Timestamps
{
    class OfflineTimestamp : BaseTimestamp
    {
        public OfflineTimestamp(DateTime timestamp) : base(timestamp)
        {
        }

        public override string MakeString()
        {
            return "Offline";
        }
    }
}