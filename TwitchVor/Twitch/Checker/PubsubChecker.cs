using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    class PubsubChecker
    {
        private readonly TwitchStatuser statuser;
        private TwitchPubSub? client;

        public DateTime? debug_LastStreamEvent = null;

        public PubsubChecker(TwitchStatuser statuser)
        {
            this.statuser = statuser;
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "PubsubChecker");
        }

        static void LogError(string message)
        {
            ColorLog.LogError(message, "PubsubChecker");
        }

        public void Start()
        {
            client = new TwitchPubSub();

            client.OnPubSubServiceConnected += PubSubServiceConnected;
            client.OnListenResponse += ListenResponse;
            client.OnPubSubServiceClosed += PubSubServiceClosed;
            client.OnPubSubServiceError += PubSubServiceError;
            client.OnStreamUp += StreamUp;
            client.OnStreamDown += StreamDown;

            client.ListenToVideoPlayback(Program.config.ChannelId);

            client.Connect();
        }

        private void PubSubServiceConnected(object? sender, EventArgs e)
        {
            var senderClient = sender as TwitchPubSub;
            if (senderClient != client || client == null)
                return;

            Log("Connected. Sending topics.");
            client.SendTopics();
        }

        private void ListenResponse(object? sender, OnListenResponseArgs e)
        {
            if (e.Successful)
            {
                Log($"Listening ({e.Topic})");
            }
            else
            {
                LogError($"Failed to listen! Response: ({e.Topic})");
            }
        }

        private void PubSubServiceClosed(object? sender, EventArgs e)
        {
            var senderClient = sender as TwitchPubSub;
            if (senderClient != client || client == null)
                return;

            Log($"Closed.");
            client.Disconnect();
            client = null;

            Task.Run(async () =>
            {
                await Task.Delay(Program.config.PubsubReconnectDelay);
                Start();
            });
        }

        private void PubSubServiceError(object? sender, OnPubSubServiceErrorArgs e)
        {
            LogError(e.Exception.ToString());
        }

        private void StreamUp(object? sender, OnStreamUpArgs e)
        {
            debug_LastStreamEvent = DateTime.UtcNow;

            var checkInfo = new TwitchCheckInfo(true, DateTime.UtcNow);

            try
            {
                statuser.StreamUp(checkInfo, true);
            }
            catch (Exception ex)
            {
                LogError($"TwitchPubSubChecker onStreamUp Exception\n{ex}");
            }
        }

        private void StreamDown(object? sender, OnStreamDownArgs e)
        {
            debug_LastStreamEvent = DateTime.UtcNow;
            
            var checkInfo = new TwitchCheckInfo(false, DateTime.UtcNow);

            try
            {
                statuser.StreamDown(checkInfo, true);
            }
            catch (Exception ex)
            {
                LogError($"TwitchPubSubChecker onStreamDown Exception\n{ex}");
            }
        }
    }
}