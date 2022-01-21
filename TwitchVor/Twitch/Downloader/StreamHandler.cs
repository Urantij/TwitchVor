using System.Linq;
using System.Net.Http;
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
        internal readonly DigitalOceanVolumeOperator? volumeOperator;

        /// <summary>
        /// UTC
        /// </summary>
        internal readonly DateTime handlerCreationDate;

        /// <summary>
        /// Сколько секунд рекламы поели.
        /// Не точное время, так как не по миссинг сегментам, а ожидаемому времени.
        /// </summary>
        internal float advertismentSeconds = 0f;

        public StreamHandler(Timestamper timestamper)
        {
            this.timestamper = timestamper;

            handlerCreationDate = DateTime.UtcNow;

            if (Program.config.Ocean != null)
            {
                string volumename = DigitalOceanVolumeOperator.GenerateVolumeName(handlerCreationDate);
                volumeOperator = new DigitalOceanVolumeOperator(Program.config.Ocean, volumename);
            }
        }

        void Log(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.Log(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        void LogError(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogError(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        internal void Start()
        {
            Log("Starting...");

            //TODO UseTempVideoWriter
            if (volumeOperator != null)
            {
                _ = volumeOperator.CreateAsync();
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

            if (volumeOperator?.Ready == false)
            {
                //Какой шанс?
                LogError("Какого хуя вольюм не готов?");
                volumeOperator.GetCreationTask!.GetAwaiter().GetResult();
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

                var thatSegments = segmentsDownloader = new SegmentsDownloader(settings, Program.config.Channel!);
                segmentsDownloader.UnknownPlaylistLineFound += UnknownPlaylistLineFound;
                segmentsDownloader.CommentPlaylistLineFound += CommentPlaylistLineFound;

                segmentsDownloader.MasterPlaylistExceptionOccured += MasterPlaylistExceptionOccured;
                segmentsDownloader.MediaPlaylistExceptionOccured += MediaPlaylistExceptionOccured;
                segmentsDownloader.SegmentDownloadExceptionOccured += SegmentDownloadExceptionOccured;

                segmentsDownloader.PlaylistEnded += PlaylistEnded;

                var thatQueue = downloadQueue = new();
                thatQueue.ItemDequeued += ItemDequeued;
                thatQueue.ExceptionOccured += QueueException;

                segmentsDownloader.SegmentArrived += SegmentArrived;

                segmentsDownloader.Update(Program.config.DownloaderClientId, Program.config.DownloaderOAuth)
                                  .ContinueWith((task) => SegmentsTokenContinuation(task, segmentsDownloader));
                /*segmentsDownloader.UpdateAccess(Program.config.DownloaderClientId, Program.config.DownloaderOAuth)
                                  .ContinueWith((task) => SegmentsTokenContinuation(task, segmentsDownloader));*/
            }
        }

        private async void SegmentsTokenContinuation(Task task, SegmentsDownloader thatSegments)
        {
            //ложку локов бы
            if (thatSegments != segmentsDownloader || thatSegments.Disposed)
                return;

            if (task.IsFaulted)
            {
                Log($"Could not update access token\n{task.Exception}");

                await Task.Delay(Program.config.SegmentAccessReupdateDelay);

                if (thatSegments != segmentsDownloader || thatSegments.Disposed)
                    return;

                _ = thatSegments.Update(Program.config.DownloaderClientId, Program.config.DownloaderOAuth)
                                .ContinueWith((task) => SegmentsTokenContinuation(task, thatSegments));
            }
            else if (task.IsCanceled)
            {
                Log($"Could not update access token: cancelled");
            }
            else
            {
                Log($"Updated access token");

                thatSegments.Start();
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

                if (volumeOperator?.Ready == true && currentVideoWriter?.temp == true)
                {
                    //переносим кал
                    Log("Volume created, moving file to new home");

                    currentVideoWriter.Wait().GetAwaiter().GetResult();

                    VideoWriter[] toMove = pastVideoWriters.Append(currentVideoWriter).ToArray();
                    foreach (var moving in toMove)
                    {
                        //Точно не нулл
                        string newFileName = FileThing.RemoveTempPrefix(moving.linkedThing.FileName);
                        string newMovingPath = Path.Combine($"/mnt/{volumeOperator.volumeName}", newFileName);

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
                    if (volumeOperator != null)
                    {
                        if (volumeOperator.Ready)
                        {
                            fileName = VideoWriter.GenerateFileName(date);
                            //TODO странно, что часть пути через комбайн, часть руками
                            path = Path.Combine($"/mnt/{volumeOperator.volumeName}", fileName);

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
                LogError($"{message} HttpException {re.Message} ({re.StatusCode})");
            }
            else
            {
                LogError($"{message}\n{e}");
            }
        }
        #endregion
    }
}