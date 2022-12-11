using Microsoft.Extensions.Logging;
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
        readonly ILogger _logger;

        public readonly HelixChecker helixChecker;
        public readonly PubsubChecker pubsubChecker;

        readonly object locker = new();

        public event EventHandler? ChannelWentOnline;
        public event EventHandler? ChannelWentOffline;

        public TwitchStatuser(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            helixChecker = new HelixChecker(loggerFactory);
            pubsubChecker = new PubsubChecker(loggerFactory);

            helixChecker.ChannelChecked += HelixChecked;
            pubsubChecker.ChannelChecked += PubsubChecked;
        }

        public void Init()
        {
            helixChecker.Start();
            pubsubChecker.Start();
        }

        private void HelixChecked(object? sender, TwitchCheckInfo arg)
        {
            if (arg.online)
            {
                StreamUp(arg, false);
            }
            else
            {
                StreamDown(arg, false);
            }
        }

        private void PubsubChecked(object? sender, TwitchCheckInfo arg)
        {
            if (arg.online)
            {
                StreamUp(arg, true);
            }
            else
            {
                StreamDown(arg, true);
            }
        }

        void StreamUp(TwitchCheckInfo check, bool trustworthy)
        {
            lock (locker)
            {
                if (!trustworthy && !IsLastCheckOld())
                    return;

                if (lastCheckInfo?.online == true)
                    return;

                lastCheckInfo = check;
            }

            _logger.LogInformation("StreamUp. {propname}: {value}", nameof(trustworthy), trustworthy);

            ChannelWentOnline?.Invoke(this, EventArgs.Empty);
        }

        void StreamDown(TwitchCheckInfo check, bool trustworthy)
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

            _logger.LogInformation("StreamDown. {propname}: {value}", nameof(trustworthy), trustworthy);

            ChannelWentOffline?.Invoke(this, EventArgs.Empty);
        }

        private bool IsLastCheckOld()
        {
            if (lastCheckInfo == null)
            {
                return true;
            }

            TimeSpan passed = DateTime.UtcNow - lastCheckInfo.checkTime;

            return passed >= trustTime;
        }
    }
}