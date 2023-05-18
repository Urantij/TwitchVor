using System.Net.WebSockets;
using System.Text.Json;
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

            var topic = client.AddPlaybackTopic(Program.config.ChannelId);
            topic.DataReceived += PlaybackReceived;

            client.Connected += Connected;
            client.ConnectionClosed += ConnectionClosed;
            client.MessageProcessingException += MessageProcessingException;
        }

        private void Connected()
        {
            _logger.LogInformation("Connected.");
        }

        private void ConnectionClosed(Exception? ex)
        {
            if (ex is WebSocketException wsE && wsE.HResult == -2147467259)
            {
                // Соединение неожиданно закрылось. Я реконнекты игнорю, наверное оно.
                _logger.LogWarning("Connection closed.");
            }
            else
            {
                _logger.LogWarning(ex, "ConnectionClosed.");
            }
        }

        private void PlaybackReceived(PlaybackData data)
        {
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
                return;
            }

            DateTime time = DateTimeOffset.FromUnixTimeSeconds((long)data.ServerTime!.Value).UtcDateTime;

            _logger.LogDebug("PlaybackReceived {status} {time}", status, time);

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

        private void MessageProcessingException((Exception exception, string message) obj)
        {
            string id = Guid.NewGuid().ToString("N");

            _logger.LogCritical(obj.exception, $"{nameof(MessageProcessingException)} {{id}}", id);

            Task.Run(async () =>
            {
                await File.WriteAllTextAsync($"{id}-error.txt", $"{obj.exception}\n\n{obj.message}");
            });
        }

        public void Start()
        {
            _logger.LogInformation("Connecting...");
            client.ConnectAsync();
        }
    }
}