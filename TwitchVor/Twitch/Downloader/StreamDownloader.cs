using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;
using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.Space;
using TwitchVor.Space.Local;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Downloader
{
    /// <summary>
    /// Отвечает за загрузку стрима куда надо.
    /// Плюс пишет инфу о полученных сегментах в бд.
    /// </summary>
    public class StreamDownloader
    {
        readonly Guid guid;
        readonly StreamDatabase db;

        readonly HttpClient httpClient;

        readonly SegmentsDownloader segmentsDownloader;
        readonly DownloadQueue downloadQueue;

        LocalSpaceProvider? tempSpace;
        readonly BaseSpaceProvider space;

        public bool Working { get; private set; }

        DateTimeOffset? lastSegmentEnd = null;

        /// <summary>
        /// Сколько секунд рекламы поели.
        /// Не точное время, так как не по миссинг сегментам, а ожидаемому времени.
        /// </summary>
        internal TimeSpan AdvertismentTime { get; private set; } = TimeSpan.Zero;

        public StreamDownloader(Guid guid, StreamDatabase db, BaseSpaceProvider space)
        {
            this.guid = guid;
            this.db = db;
            this.space = space;

            httpClient = new HttpClient(new HttpClientHandler()
            {
                Proxy = null,
                UseProxy = false
            });

            var settings = new SegmentsDownloaderSettings()
            {
                preferredFps = Program.config.PreferedVideoFps,
                preferredQuality = Program.config.PreferedVideoQuality,

                takeOnlyPreferredQuality = Program.config.TakeOnlyPrefered,
            };

            segmentsDownloader = new SegmentsDownloader(httpClient, settings, Program.config.Channel!, Program.config.Downloader.ClientId, Program.config.Downloader.OAuth);
            segmentsDownloader.UnknownPlaylistLineFound += UnknownPlaylistLineFound;
            segmentsDownloader.CommentPlaylistLineFound += CommentPlaylistLineFound;

            segmentsDownloader.MasterPlaylistExceptionOccured += MasterPlaylistExceptionOccured;
            segmentsDownloader.MediaPlaylistExceptionOccured += MediaPlaylistExceptionOccured;

            segmentsDownloader.TokenAcquired += TokenAcquired;
            segmentsDownloader.TokenAcquiringExceptionOccured += TokenAcquiringExceptionOccured;

            segmentsDownloader.MediaQualitySelected += MediaQualitySelected;
            segmentsDownloader.PlaylistEnded += PlaylistEnded;

            downloadQueue = new DownloadQueue(Program.config.SegmentDownloaderTimeout);
            downloadQueue.ItemDequeued += ItemDequeued;

            segmentsDownloader.SegmentArrived += SegmentArrived;
        }

        void Log(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.Log(message, $"StreamDownloader");
        }

        void LogWarning(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogWarning(message, $"StreamDownloader");
        }

        void LogError(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogError(message, $"StreamDownloader");
        }

        internal void Start()
        {
            Log("Starting...");

            Working = true;

            segmentsDownloader.Start();
        }

        internal void Suspend()
        {
            Working = false;

            segmentsDownloader.Stop();
        }

        internal void Resume()
        {
            Working = true;

            segmentsDownloader.Start();
        }

        internal async Task CloseAsync()
        {
            await space.CloseAsync();

            segmentsDownloader.Dispose();
            downloadQueue.Dispose();

            httpClient.Dispose();
        }

        /// <summary>
        /// Переносит все сегменты из временного спейпса в целевой, и закрывает временный спейс.
        /// </summary>
        /// <param name="targetSpace"></param>
        /// <returns></returns>
        async Task TransferSpaceContentAsync(BaseSpaceProvider targetSpace)
        {
            SegmentDb[] segments = db.LoadAllSegments();

            await tempSpace!.CloseAsync();

            FileStream fs = tempSpace.OpenReadFs();

            foreach (var segment in segments)
            {
                try
                {
                    await targetSpace.PutDataAsync(segment.Id, fs, segment.Size);
                }
                catch (Exception exception)
                {
                    LogException("Unable to putdata", exception);
                }
            }

            var destroySpace = tempSpace;
            tempSpace = null;

            _ = Task.Run(async () =>
            {
                await destroySpace.CloseAsync();
                await destroySpace.DestroyAsync();
            });
        }

        private async void SegmentArrived(object? sender, StreamSegment segment)
        {
            if (!segment.IsLive)
            {
                AdvertismentTime += TimeSpan.FromSeconds(segment.duration);
                return;
            }

            QueueItem queueItem = downloadQueue.Queue(segment, new MemoryStream());

            try
            {
                await downloadQueue.DownloadAsync(httpClient, queueItem);
            }
            catch (Exception e)
            {
                SegmentDownloadExceptionOccured(sender, e);
            }
        }

        private async void ItemDequeued(object? sender, QueueItem qItem)
        {
            try
            {
                if (qItem.Written)
                {
                    BaseSpaceProvider spaceToWrite;

                    if (space.Ready)
                    {
                        if (tempSpace == null)
                        {
                            spaceToWrite = space;
                        }
                        else
                        {
                            // Место только стало доступным
                            // Значит, нам нужно прочитать из базы сегменты, читать их из файла и перенаправить
                            Log("Space created, moving file to new home");

                            TransferSpaceContentAsync(space).GetAwaiter().GetResult();

                            spaceToWrite = space;
                        }
                    }
                    else
                    {
                        if (tempSpace == null)
                        {
                            tempSpace = new LocalSpaceProvider(guid, DependencyProvider.MakeLocalSpacePath(guid, true));
                            tempSpace.InitAsync().GetAwaiter().GetResult();
                        }

                        spaceToWrite = tempSpace;
                    }

                    int id = db.AddSegment(qItem.segment.mediaSequenceNumber, qItem.segment.programDate, qItem.bufferWriteStream.Length, qItem.segment.duration);
                    qItem.bufferWriteStream.Position = 0;

                    try
                    {
                        if (spaceToWrite.AsyncUpload)
                        {
                            await spaceToWrite.PutDataAsync(id, qItem.bufferWriteStream, qItem.bufferWriteStream.Length);
                        }
                        else
                        {
                            spaceToWrite.PutDataAsync(id, qItem.bufferWriteStream, qItem.bufferWriteStream.Length).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception exception)
                    {
                        LogException("Unable to putdata", exception);
                    }

                    if (lastSegmentEnd != null)
                    {
                        var difference = qItem.segment.programDate - lastSegmentEnd.Value;

                        if (difference >= Program.config.MinimumSegmentSkipDelay)
                        {
                            Log($"Skip Detected! Skipped {difference.TotalSeconds:N0} seconds :(");

                            await db.AddSkipAsync(lastSegmentEnd.Value, qItem.segment.programDate);
                        }
                    }

                    lastSegmentEnd = qItem.segment.programDate.AddSeconds(qItem.segment.duration);
                }
                else
                {
                    // пропущен сегмент

                    Log($"Missing downloading segment {qItem.segment.title}");
                }
            }
            finally
            {
                await qItem.bufferWriteStream.DisposeAsync();
            }
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

                if (downloader.Access != e)
                    return;

                //по факту лишние проверки, ну да ладно
                if (downloader.Disposed || !Working)
                    return;

                Log("Dropping access token on schedule...");
                downloader.DropToken();
            });
        }

        private void MediaQualitySelected(object? sender, VariantStream e)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            if (downloader.LastVideo == e.streamInfTag.video)
                return;

            db.AddVideoFormat(e.streamInfTag.video!, DateTimeOffset.UtcNow);

            Log($"New quality selected: {e.streamInfTag.video} ({downloader.LastVideo ?? "null"})");
        }

        #region Logs
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

        private void TokenAcquiringExceptionOccured(object? sender, Exception e)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            LogException($"TokenAcq Failed ({downloader.TokenAcquiranceFailedAttempts})", e);
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
                    if (io.Message == "Unable to read data from the transport connection: Connection reset by peer.")
                    {
                        LogError($"{message} Connection reset by peer.");
                    }
                    else
                    {
                        LogError($"{message} HttpRequestException.IOException: \"{io.Message}\"");
                    }
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