using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    /// <summary>
    /// Этот класс следит за тем, когда стрим начинается и заканчивается, а также когда меняется его название или категория
    /// </summary>
    class TwitchStatuser
    {
        /// <summary>
        /// Ивент с хеликса приносит устаревшую инфу. И если инфа противоречит, и она новая, то я подожду
        /// </summary>
        private readonly static TimeSpan trustTime = TimeSpan.FromSeconds(60);

        TwitchCheckInfo? lastCheckInfo = null;

        public readonly HelixChecker helixChecker;
        public readonly PubsubChecker pubsubChecker;

        readonly object locker = new();

        public event EventHandler? ChannelWentOnline;
        public event EventHandler? ChannelWentOffline;

        public TwitchStatuser()
        {
            helixChecker = new HelixChecker(this);
            pubsubChecker = new PubsubChecker(this);
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "TwitchStatuser");
        }

        public void Init()
        {
            helixChecker.Start();
            pubsubChecker.Start();
        }

        public void StreamUp(TwitchCheckInfo check, bool trustworthy)
        {
            lock (locker)
            {
                if (!trustworthy && lastCheckInfo != null && !IsLastCheckOld())
                    return;

                if (lastCheckInfo?.online == true)
                    return;

                lastCheckInfo = check;
            }

            Log($"StreamUp. {nameof(trustworthy)}: {trustworthy}");

            ChannelWentOnline?.Invoke(this, EventArgs.Empty);
        }

        public void StreamDown(TwitchCheckInfo check, bool trustworthy)
        {
            lock (locker)
            {
                if (lastCheckInfo == null)
                    return;

                if (!trustworthy && !IsLastCheckOld())
                    return;

                if (lastCheckInfo.online != true)
                    return;

                lastCheckInfo = check;
            }

            Log($"StreamDown. {nameof(trustworthy)}: {trustworthy}");

            ChannelWentOffline?.Invoke(this, EventArgs.Empty);
        }

        private bool IsLastCheckOld()
        {
            if (lastCheckInfo == null)
            {
                return true;
            }

            var passed = DateTime.UtcNow - lastCheckInfo.checkTime;

            return passed >= trustTime;
        }
    }
}