using Microsoft.Extensions.Logging;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    class PubsubChecker : BaseChecker
    {
        private TwitchPubSub? client;

        public DateTime? debug_LastStreamEvent = null;

        public PubsubChecker(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public void Start()
        {
            var thisClient = client = new TwitchPubSub();
            try
            {
                thisClient.OnPubSubServiceConnected += PubSubServiceConnected;
                thisClient.OnListenResponse += ListenResponse;
                thisClient.OnPubSubServiceClosed += PubSubServiceClosed;
                thisClient.OnPubSubServiceError += PubSubServiceError;
                thisClient.OnStreamUp += StreamUp;
                thisClient.OnStreamDown += StreamDown;

                thisClient.ListenToVideoPlayback(Program.config.ChannelId);

                _logger.LogInformation("Connecting...");
                thisClient.Connect();
            }
            catch (Exception e)
            {
                thisClient.OnPubSubServiceConnected -= PubSubServiceConnected;
                thisClient.OnListenResponse -= ListenResponse;
                thisClient.OnPubSubServiceClosed -= PubSubServiceClosed;
                thisClient.OnPubSubServiceError -= PubSubServiceError;
                thisClient.OnStreamUp -= StreamUp;
                thisClient.OnStreamDown -= StreamDown;

                _logger.LogError(e, "Ошибка при подключении.");

                PubSubServiceClosed(thisClient, EventArgs.Empty);
            }
        }

        private void PubSubServiceConnected(object? sender, EventArgs e)
        {
            var senderClient = sender as TwitchPubSub;
            if (senderClient != client || client == null)
                return;

            _logger.LogInformation("Connected. Sending topics.");
            senderClient!.SendTopics(); //не может быть нул, сверху проверка
        }

        private void ListenResponse(object? sender, OnListenResponseArgs e)
        {
            if (e.Successful)
            {
                _logger.LogInformation("Listening ({Topic})", e.Topic);
            }
            else
            {
                _logger.LogError("Failed to listen! Response: ({Topic})", e.Topic);
            }
        }

        private void PubSubServiceClosed(object? sender, EventArgs e)
        {
            var senderClient = sender as TwitchPubSub;
            if (senderClient != client || client == null)
                return;

            _logger.LogInformation($"Closed.");
            client = null;
            senderClient!.Disconnect(); //не может быть нул. Сверху проверка

            Task.Run(async () =>
            {
                await Task.Delay(Program.config.PubsubReconnectDelay);

                //Такого не должно быть, но я ебал эту либу
                if (client != null)
                    return;

                Start();
            });
        }

        private void PubSubServiceError(object? sender, OnPubSubServiceErrorArgs e)
        {
            _logger.LogError(e.Exception, "PubSubServiceError");
        }

        private void StreamUp(object? sender, OnStreamUpArgs e)
        {
            debug_LastStreamEvent = DateTime.UtcNow;

            var checkInfo = new TwitchCheckInfo(true, DateTime.UtcNow);

            try
            {
                OnChannelChecked(checkInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onStreamUp");
            }
        }

        private void StreamDown(object? sender, OnStreamDownArgs e)
        {
            debug_LastStreamEvent = DateTime.UtcNow;

            var checkInfo = new TwitchCheckInfo(false, DateTime.UtcNow);

            try
            {
                OnChannelChecked(checkInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "onStreamDown");
            }
        }
    }
}