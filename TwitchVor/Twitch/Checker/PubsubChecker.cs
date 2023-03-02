using Microsoft.Extensions.Logging;
using TwitchSimpleLib.Pubsub;
using TwitchSimpleLib.Pubsub.Payloads.Playback;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    class PubsubChecker : BaseChecker
    {
        private readonly TwitchPubsubClient client;

        public DateTime? debug_LastStreamEvent = null;

        public PubsubChecker(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            client = new TwitchPubsubClient(new TwitchPubsubClientOpts(), loggerFactory);
            if (Program.config.ChannelId is null)
                throw new NullReferenceException(nameof(Program.config.ChannelId));

            client.AddPlaybackTopic(Program.config.ChannelId);

            client.Connected += Connected;
            client.ConnectionClosed += ConnectionClosed;
            client.PlaybackReceived += PlaybackReceived;
        }

        private void Connected()
        {
            _logger.LogInformation("Connected.");
        }

        private void ConnectionClosed(Exception? ex)
        {
            _logger.LogWarning(ex, "ConnectionClosed.");
        }

        private void PlaybackReceived((string channelId, PlaybackData) args)
        {
            PlaybackData data = args.Item2;

            bool status;
            if (data.Type == "stream-up")
            {
                status = true;
            }
            else if (data.Type == "stream-down")
            {
                status = false;
            }
            else
            {
                _logger.LogError("Непонятный тип в плейбеке {type}", data.Type);
                return;
            }

            DateTime time = DateTimeOffset.FromUnixTimeSeconds(data.ServerTime).UtcDateTime;

            TwitchCheckInfo checkInfo = new(status, time);
            try
            {
                OnChannelChecked(checkInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaybackReceived");
            }
        }

        public void Start()
        {
            _logger.LogInformation("Connecting...");
            client.ConnectAsync();
        }
    }
}