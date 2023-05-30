namespace TwitchVor.Vvideo.Timestamps
{
    class GameTimestamp : BaseTimestamp
    {
        public readonly string title;
        public readonly string? gameName;
        public readonly string gameId;

        public GameTimestamp(string title, string? gameName, string gameId, DateTime timestamp) : base(timestamp)
        {
            this.title = title;
            this.gameName = gameName;
            this.gameId = gameId;
        }

        public override string MakeString()
        {
            return $"{title} // {gameName ?? "???"} ({gameId})";
        }
    }
}