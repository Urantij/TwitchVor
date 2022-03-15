using TwitchVor.Twitch.Checker;
using TwitchVor.Utility;
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

        static void Log(string message)
        {
            ColorLog.Log(message, "SM");
        }

        static void LogError(string message)
        {
            ColorLog.LogError(message, "SM");
        }

        public void EndStream()
        {
            lock (locker)
            {
                if (currentStream == null)
                {
                    Log("There is no stream.");
                    return;
                }

                if (!currentStream.Suspended)
                {
                    Log("Stream isnt suspended.");
                    return;
                }

                StartStreamFinishing();
            }
        }

        /// <summary>
        /// Текущий стрим заменяется на нул и у него вызвыается финиш
        /// </summary>
        private void StartStreamFinishing()
        {
            StreamHandler finishingStream;
            lock (locker)
            {
                //не может быть нул
                finishingStream = currentStream!;

                currentStream = null;
                ClearCurrentCancellationSource();

                currentStamper.Stop();
                currentStamper = new(Program.statuser.helixChecker);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await finishingStream.FinishAsync();
                }
                catch (Exception e)
                {
                    LogError($"Could not finish stream:\n{e}");

                    if (Program.emailer != null && Program.config.Email!.NotifyOnCriticalError)
                    {
                        await Program.emailer.SendCriticalErrorAsync("Не получилось зафинишировать стрим");
                    }
                }
            });
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
                    ClearCurrentCancellationSource();

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
            catch { return; }

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
            catch { return; }

            lock (locker)
            {
                if (thatSource.IsCancellationRequested)
                    return;

                StartStreamFinishing();
            }
        }

        private void ClearCurrentCancellationSource()
        {
            //этого не должно быть, чтобы иде не ныла
            if (currentStreamOfflineCancelSource == null)
                return;

            try { currentStreamOfflineCancelSource.Cancel(); } catch {};
            currentStreamOfflineCancelSource.Dispose();
            currentStreamOfflineCancelSource = null;
        }
    }
}