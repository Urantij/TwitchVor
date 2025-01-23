using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;
using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Downloader
{
    /// <summary>
    /// Следит за появлением стрима чтобы создать хендлер
    /// </summary>
    class StreamsManager
    {
        readonly ILogger _logger;
        readonly ILoggerFactory _loggerFactory;

        CancellationTokenSource? currentStreamOfflineCancelSource;
        StreamHandler? currentStream;
        Timestamper currentStamper;

        private readonly object locker = new();

        public StreamsManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());
            _loggerFactory = loggerFactory;

            currentStamper = new(_loggerFactory);
            Program.statuser.helixChecker.ChannelChecked += currentStamper.HelixChecker_ChannelChecked;

            Program.statuser.ChannelWentOnline += StatuserOnline;
            Program.statuser.ChannelWentOffline += StatuserOffline;
        }

        public void EndStream()
        {
            lock (locker)
            {
                if (currentStream == null)
                {
                    _logger.LogWarning("There is no stream.");
                    return;
                }

                if (!currentStream.Suspended)
                {
                    _logger.LogWarning("Stream isnt suspended.");
                    return;
                }

                StartStreamFinishing();
            }
        }

        /// <summary>
        /// Текущий стрим заменяется на нул и у него вызывается финиш
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

                Program.statuser.helixChecker.ChannelChecked -= currentStamper.HelixChecker_ChannelChecked;

                currentStamper = new(_loggerFactory);
                Program.statuser.helixChecker.ChannelChecked += currentStamper.HelixChecker_ChannelChecked;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Завершаем стрим {guid}...", finishingStream.guid);

                    await finishingStream.FinishAsync();

                    StreamFinisher finisher = new(finishingStream, _loggerFactory);
                    await finisher.DoAsync();

                    _logger.LogInformation("Стрим {guid} завершён.", finishingStream.guid);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Не удалось зафинишировать стрим.");

                    if (Program.emailer != null)
                        await Program.emailer.SendCriticalErrorAsync("Не получилось зафинишировать стрим");
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
                    currentStream = new StreamHandler(currentStamper, _loggerFactory);
                    _ = currentStream.StartAsync();
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
                return;
            }

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

            try
            {
                currentStreamOfflineCancelSource.Cancel();
            }
            catch
            {
            }

            ;
            currentStreamOfflineCancelSource.Dispose();
            currentStreamOfflineCancelSource = null;
        }
    }
}