using System.Linq;
using System.Net.Http;
using PlaylistParser.Models;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;
using TwitchVor.Finisher;
using TwitchVor.Ocean;
using TwitchVor.Utility;
using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Downloader
{
    /// <summary>
    /// Когда появляется стрим, программа создаёт эту хуйню.
    /// И она отвечает за дальнейшее развитие событий.
    /// </summary>
    class StreamHandler
    {
        internal bool Finished { get; private set; } = false;
        internal bool Suspended { get; private set; } = false;

        SegmentsDownloader? segmentsDownloader;
        DownloadQueue? downloadQueue;

        internal VideoWriter? currentVideoWriter;
        internal readonly List<VideoWriter> pastVideoWriters = new();

        internal readonly Timestamper timestamper;

        /// <summary>
        /// нул, если не облачный
        /// </summary>
        readonly OceanCreds? oceanCreds;
        internal DigitalOceanVolumeOperator? volumeOperator2;

        bool IsCloud => oceanCreds != null;

        /// <summary>
        /// UTC
        /// </summary>
        internal readonly DateTime handlerCreationDate;

        /// <summary>
        /// Сколько секунд рекламы поели.
        /// Не точное время, так как не по миссинг сегментам, а ожидаемому времени.
        /// </summary>
        internal float advertismentSeconds = 0f;

        public StreamHandler(Timestamper timestamper, OceanCreds? oceanCreds)
        {
            this.timestamper = timestamper;
            this.oceanCreds = oceanCreds;

            handlerCreationDate = DateTime.UtcNow;
        }

        void Log(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.Log(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        void LogWarning(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogWarning(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        void LogError(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogError(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        internal void Start()
        {
            Log("Starting...");

            if (IsCloud)
            {
                string volumename = DigitalOceanVolumeCreator.GenerateVolumeName(handlerCreationDate);
                //не знает, что IsCloud это проверка на нулл
                var creator = new DigitalOceanVolumeCreator(oceanCreds!, volumename);

                creator.CreateAsync().ContinueWith(VolumeAttachedHandler);
            }

            LaunchSegmentsDownloader();
        }

        /// <summary>
        /// Остановим загрузчики от ддоса твича
        /// </summary>
        internal void Suspend()
        {
            Log("Suspending...");

            Suspended = true;

            /* Может быть нул?
             * Эти интернал хуйни вызываются в локе, при этом порядок довольно простой
             * Ставлю краш, что нет. */
            segmentsDownloader!.Stop();
        }

        /// <summary>
        /// Загрузчики должны были умереть, так что запустим их
        /// </summary>
        internal void Resume()
        {
            Log("Resuming...");

            Suspended = false;

            LaunchSegmentsDownloader();
        }

        /// <summary>
        /// Конец.
        /// </summary>
        internal async Task FinishAsync()
        {
            Log("Finishing...");

            Finished = true;

            if (IsCloud && volumeOperator2 == null)
            {
                //Какой шанс?
                LogError("Какого хуя вольюм не готов?");
                while (volumeOperator2 == null)
                {
                    await Task.Delay(5000);
                }
                LogError("Пиздец.");
            }

            currentVideoWriter?.CloseAsync().GetAwaiter().GetResult();

            segmentsDownloader!.Dispose();
            segmentsDownloader = null;

            downloadQueue!.Dispose();
            downloadQueue = null;

            StreamFinisher finisher = new(this);
            await finisher.Finish();
        }

        private void LaunchSegmentsDownloader()
        {
            if (segmentsDownloader != null)
            {
                segmentsDownloader.Start();
            }
            else
            {
                var settings = new SegmentsDownloaderSettings()
                {
                    preferredFps = Program.config.PreferedVideoFps,
                    preferredQuality = Program.config.PreferedVideoQuality,

                    takeOnlyPreferredQuality = true,
                };

                var thatSegments = segmentsDownloader = new SegmentsDownloader(settings, Program.config.Channel!, Program.config.DownloaderClientId, Program.config.DownloaderOAuth);
                segmentsDownloader.UnknownPlaylistLineFound += UnknownPlaylistLineFound;
                segmentsDownloader.CommentPlaylistLineFound += CommentPlaylistLineFound;

                segmentsDownloader.MasterPlaylistExceptionOccured += MasterPlaylistExceptionOccured;
                segmentsDownloader.MediaPlaylistExceptionOccured += MediaPlaylistExceptionOccured;
                segmentsDownloader.SegmentDownloadExceptionOccured += SegmentDownloadExceptionOccured;

                segmentsDownloader.TokenAcquired += TokenAcquired;
                segmentsDownloader.TokenAcquiringException += TokenAcquiringException;

                segmentsDownloader.MediaQualitySelected += MediaQualitySelected;
                segmentsDownloader.PlaylistEnded += PlaylistEnded;

                var thatQueue = downloadQueue = new();
                thatQueue.ItemDequeued += ItemDequeued;
                thatQueue.ExceptionOccured += QueueException;

                segmentsDownloader.SegmentArrived += SegmentArrived;

                segmentsDownloader.Start();
            }
        }

        private void SegmentArrived(object? sender, StreamSegment e)
        {
            SegmentsDownloader? thatSegments = sender as SegmentsDownloader;
            var thatQueue = downloadQueue;

            if (thatSegments != segmentsDownloader || thatSegments == null || thatQueue == null)
                return;

            if (!e.IsLive)
            {
                advertismentSeconds += e.duration;
                return;
            }

            _ = thatQueue.Download(e, thatSegments, new MemoryStream(), Program.config.SegmentDownloaderTimeout);
            thatQueue.Queue(e);
        }

        private void ItemDequeued(object? sender, QueueItem e)
        {
            DownloadQueue? thatQueue = sender as DownloadQueue;

            if (thatQueue != downloadQueue || thatQueue == null)
                return;

            if (e.written)
            {
                e.bufferWriteStream.Position = 0;

                //Оператор стал доступен, нужно перенест хуйню
                if (volumeOperator2 != null && currentVideoWriter?.temp == true)
                {
                    //переносим кал
                    Log("Volume created, moving file to new home");

                    currentVideoWriter.Wait().GetAwaiter().GetResult();

                    VideoWriter[] toMove = pastVideoWriters.Append(currentVideoWriter).ToArray();
                    foreach (var moving in toMove)
                    {
                        //Точно не нулл
                        string newFileName = FileThing.RemoveTempPrefix(moving.linkedThing.FileName);
                        string newMovingPath = Path.Combine($"/mnt/{volumeOperator2.volumeName}", newFileName);

                        Log($"Moving {moving.linkedThing.FileName}");

                        if (moving == currentVideoWriter)
                            moving.CloseFileStream();
                        //понадеюсь, что тут не нужно ждать его закрытия. Я просто хочу верить.
                        File.Move(moving.linkedThing.FilePath, newMovingPath);

                        moving.linkedThing.SetPath(newMovingPath);
                        moving.linkedThing.SetName(newFileName);
                        moving.temp = false;

                        if (moving == currentVideoWriter)
                            moving.OpenFileStream();
                    }
                }

                if (currentVideoWriter == null || currentVideoWriter.linkedThing.quality != e.segment.qualityStr ||
                    currentVideoWriter.linkedThing.estimatedDuration >= Program.config.MaximumVideoDuration.TotalSeconds ||
                    currentVideoWriter.linkedThing.estimatedSize >= Program.config.MaximumVideoSize)
                {
                    DateTime date = DateTime.UtcNow;

                    string fileName;
                    string path;
                    bool isTemp;
                    if (IsCloud)
                    {
                        if (volumeOperator2 != null)
                        {
                            fileName = VideoWriter.GenerateFileName(date);
                            //TODO странно, что часть пути через комбайн, часть руками
                            path = Path.Combine($"/mnt/{volumeOperator2.volumeName}", fileName);

                            isTemp = false;
                        }
                        else
                        {
                            fileName = FileThing.AddTempPrefix(VideoWriter.GenerateFileName(date));
                            path = Path.Combine(Program.config.VideosDirectoryName, fileName);

                            isTemp = true;
                        }
                    }
                    else
                    {
                        fileName = VideoWriter.GenerateFileName(date);
                        path = Path.Combine(Program.config.VideosDirectoryName, fileName);

                        isTemp = false;
                    }

                    Log($"Creating new video writer {fileName}");
                    FileThing fileThing = new(path, fileName, e.segment.qualityStr);

                    if (currentVideoWriter != null)
                    {
                        _ = currentVideoWriter.CloseAsync();
                        pastVideoWriters.Add(currentVideoWriter);
                        currentVideoWriter = null;
                    }

                    currentVideoWriter = new VideoWriter(fileThing, isTemp);
                }

                currentVideoWriter.Write(e);
            }
            else
            {
                //пропущен сегмент
                e.bufferWriteStream.Dispose();

                Log($"Missing downloading segment {e.segment.title}");
            }
        }

        private void VolumeAttachedHandler(Task<DigitalOceanVolumeOperator> task)
        {
            //как то впадлу думать что делать, если выпала ошибка
            //TODO подумать

            volumeOperator2 = task.Result;
        }

        private void TokenAcquired(object? sender, AccessToken e)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            string fails = $" ({downloader.TokenAcquiranceFailedAttempts} failed)";

            if (e.parsedValue.expires == null)
            {
                if (Program.config.DownloaderForceTokenChange)
                {
                    LogError($"Got playback token! no {nameof(e.parsedValue.expires)}" + fails);
                }
                else
                {
                    LogWarning($"Got playback token! no {nameof(e.parsedValue.expires)}" + fails);
                }

                return;
            }

            var left = DateTimeOffset.FromUnixTimeSeconds(e.parsedValue.expires.Value) - DateTimeOffset.UtcNow;

            Log($"Got playback token! left {left.TotalMinutes} minutes" + fails);

            if (!Program.config.DownloaderForceTokenChange)
                return;

            Task.Run(async () =>
            {
                /* в тевории стрим может уже закончится, кстати.
                 * но один лишний таск это похуй, я думаю
                 * TODO Добавить локов, чтобы исключить околоневозможный шанс пересечения интересов */
                await Task.Delay(left - TimeSpan.FromSeconds(5));

                //по факту лишние проверки, ну да ладно
                if (segmentsDownloader?.Disposed != false || Suspended || Finished)
                {
                    return;
                }

                Log("Dropping access token on schedule...");
                segmentsDownloader.DropToken();
            });
        }

        #region Logs
        private void QueueException(object? sender, Exception e)
        {
            LogException($"Download Exception", e);
        }

        private void UnknownPlaylistLineFound(object? sender, LineEventArgs e)
        {
            Log($"Unknown line ({e.Master}): \"{e.Line}\"");
        }

        private void CommentPlaylistLineFound(object? sender, LineEventArgs e)
        {
            Log($"Comment line ({e.Master}): \"{e.Line}\"");
        }

        private void MasterPlaylistExceptionOccured(object? sender, Exception e)
        {
            LogException($"Master Exception", e);
        }

        private void MediaPlaylistExceptionOccured(object? sender, Exception e)
        {
            LogException($"Media Exception", e);
        }

        private void SegmentDownloadExceptionOccured(object? sender, Exception e)
        {
            LogException($"Segment Exception", e);
        }

        private void TokenAcquiringException(object? sender, Exception e)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            LogException($"TokenAcq Failed ({downloader.TokenAcquiranceFailedAttempts})", e);
        }

        private void MediaQualitySelected(object? sender, VariantStream e)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            if (downloader.LastVideo == e.streamInfTag.video)
                return;

            Log($"New quality selected: {e.streamInfTag.video} ({downloader.LastVideo ?? "null"})");
        }

        private void PlaylistEnded(object? sender, EventArgs e)
        {
            Log("Playlist End");
        }

        private void LogException(string message, Exception e)
        {
            if (e is BadCodeException be)
            {
                LogError($"{message} Bad Code ({be.statusCode})");
            }
            else if (e is HttpRequestException re)
            {
                if (re.InnerException is IOException io)
                {
                    LogError($"{message} HttpRequestException.IOException: \"{io.Message}\"");
                }
                else
                {
                    LogError($"{message} HttpRequestException\n{re}");
                }
            }
            else
            {
                LogError($"{message}\n{e}");
            }
        }
        #endregion
    }
}