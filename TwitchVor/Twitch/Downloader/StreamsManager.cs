using TwitchVor.Twitch.Checker;
using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Downloader
{
    /// <summary>
    /// Следит за появлением стрима чтобы создать хендлер
    /// </summary>
    class StreamsManager
    {
        CancellationTokenSource? currentStreamOfflineCancelSource;
        StreamHandler? currentStream;
        Timestamper currentStamper;

        private readonly object locker = new();

        public StreamsManager()
        {
            currentStamper = new(Program.statuser.helixChecker);

            Program.statuser.ChannelWentOnline += StatuserOnline;
            Program.statuser.ChannelWentOffline += StatuserOffline;
        }

        private void StatuserOnline(object? sender, EventArgs e)
        {
            lock (locker)
            {
                if (currentStream != null)
                {
                    /* Может ли сурс быть не нул?
                     * Чтобы снова сработал онлайн, нужно, чтобы сработал офлаин.
                     * Который всегда ставит сурс.
                     * Я ставлю краш программы на то, что тут всегда не нулл. */
                    currentStreamOfflineCancelSource!.Cancel();
                    currentStreamOfflineCancelSource = null;

                    if (currentStream.Suspended)
                    {
                        currentStream.Resume();
                    }
                }
                else
                {
                    currentStream = new StreamHandler(currentStamper, Program.config.Ocean);
                    currentStream.Start();
                }
            }
        }

        private async void StatuserOffline(object? sender, EventArgs e)
        {
            CancellationTokenSource thatSource;
            lock (locker)
            {
                if (currentStream == null)
                    return;

                thatSource = currentStreamOfflineCancelSource = new CancellationTokenSource();
            }

            try
            {
                await Task.Delay(Program.config.StreamContinuationCheckTime, thatSource.Token);
            }
            catch
            {
                thatSource.Dispose();
                return;
            }

            lock (locker)
            {
                if (thatSource.IsCancellationRequested)
                    return;

                currentStream.Suspend();
            }

            try
            {
                await Task.Delay(Program.config.StreamRestartCheckTime, thatSource.Token);
            }
            catch
            {
                thatSource.Dispose();
                return;
            }

            StreamHandler finishingStream;
            lock (locker)
            {
                if (thatSource.IsCancellationRequested)
                    return;

                finishingStream = currentStream;

                currentStream = null;
                currentStreamOfflineCancelSource = null;

                currentStamper.Stop();
                currentStamper = new(Program.statuser.helixChecker);
            }

            _ = Task.Run(async () =>
            {
                await finishingStream.FinishAsync();
            });


            thatSource.Dispose();
        }
    }
}