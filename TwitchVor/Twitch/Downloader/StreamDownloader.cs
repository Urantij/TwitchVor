using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExtM3UPlaylistParser.Models;
using Microsoft.Extensions.Logging;
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
        readonly ILogger _logger;

        readonly Guid guid;
        readonly StreamDatabase db;

        readonly HttpClient httpClient;

        readonly SegmentsDownloader segmentsDownloader;
        readonly DownloadQueue downloadQueue;

        LocalSpaceProvider? tempSpace;
        readonly BaseSpaceProvider space;
        readonly ILoggerFactory _loggerFactory;

        public bool Working { get; private set; }

        StreamSegment? lastSegment = null;

        /// <summary>
        /// Сколько секунд рекламы поели.
        /// Не точное время, так как не по миссинг сегментам, а ожидаемому времени.
        /// </summary>
        internal TimeSpan AdvertismentTime { get; private set; } = TimeSpan.Zero;

        public StreamDownloader(Guid guid, StreamDatabase db, BaseSpaceProvider space, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.guid = guid;
            this.db = db;
            this.space = space;
            this._loggerFactory = loggerFactory;
            httpClient = new HttpClient(new HttpClientHandler()
            {
                Proxy = null,
                UseProxy = false
            });

            var settings = new SegmentsDownloaderSettings()
            {
                preferredFps = Program.config.PreferedVideoFps,
                preferredResolution = Resolution.Parse(Program.config.PreferedVideoResolution),

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

        internal void Start()
        {
            _logger.LogInformation("Starting...");

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
                            _logger.LogInformation("Space created, moving file to new home");

                            TransferSpaceContentAsync(space).GetAwaiter().GetResult();

                            spaceToWrite = space;
                        }
                    }
                    else
                    {
                        if (tempSpace == null)
                        {
                            tempSpace = new LocalSpaceProvider(guid, _loggerFactory, DependencyProvider.MakeLocalSpacePath(guid, true));
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

                    if (lastSegment != null)
                    {
                        var lastSegmentEnd = lastSegment.programDate.AddSeconds(lastSegment.duration);

                        var difference = qItem.segment.programDate - lastSegmentEnd;

                        if (difference >= Program.config.MinimumSegmentSkipDelay)
                        {
                            _logger.LogWarning("Skip Detected! Skipped {TotalSeconds:N0} seconds ({lastSegmentId} -> {segmentId}) :(", difference.TotalSeconds, lastSegment.mediaSequenceNumber, qItem.segment.mediaSequenceNumber);

                            await db.AddSkipAsync(lastSegmentEnd, qItem.segment.programDate);
                        }
                    }

                    lastSegment = qItem.segment;
                }
                else
                {
                    // пропущен сегмент

                    _logger.LogWarning("Missing downloading segment {title}", qItem.segment.title);
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
                    _logger.LogError("Got no playback token! {fails}", fails);
                }
                else
                {
                    _logger.LogWarning("Got no playback token! {fails}", fails);
                }

                return;
            }

            var left = DateTimeOffset.FromUnixTimeSeconds(e.parsedValue.expires.Value) - DateTimeOffset.UtcNow;

            _logger.LogInformation("Got playback token! left {TotalMinutes:N1} minutes ({fails})", left.TotalMinutes, fails);

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

                _logger.LogInformation("Dropping access token on schedule...");
                downloader.DropToken();
            });
        }

        private void MediaQualitySelected(object? sender, MediaQualitySelectedEventArgs args)
        {
            //да не может он быть нулл.
            var downloader = (SegmentsDownloader)sender!;

            if (downloader.LastStreamQuality?.Same(args.Quality) == true)
                return;

            db.AddVideoFormat($"{args.Quality.resolution.width}x{args.Quality.resolution.height}:{args.Quality.fps}", DateTimeOffset.UtcNow);

            if (downloader.LastStreamQuality == null)
            {
                _logger.LogInformation("Quality selected: {height}:{fps}", args.Quality.resolution.height, args.Quality.fps);
            }
            else
            {
                _logger.LogWarning("New quality selected: {height}:{fps} ({lastHeight}:{lastFps})", args.Quality.resolution.height, args.Quality.fps, downloader.LastStreamQuality.resolution.height, downloader.LastStreamQuality.fps);
            }
        }

        #region Logs
        private void UnknownPlaylistLineFound(object? sender, LineEventArgs e)
        {
            _logger.LogWarning("Unknown line ({master}): \"{line}\"", e.Master, e.Line);
        }

        private void CommentPlaylistLineFound(object? sender, LineEventArgs e)
        {
            _logger.LogWarning("Comment line ({master}): \"{line}\"", e.Master, e.Line);
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
            _logger.LogInformation("Playlist End");
        }

        private void LogException(string message, Exception e)
        {
            if (e is BadCodeException be)
            {
                _logger.LogError("{message} Bad Code ({statusCode})", message, be.statusCode);
            }
            else if (e is HttpRequestException re)
            {
                if (re.InnerException is IOException io)
                {
                    if (io.Message == "Unable to read data from the transport connection: Connection reset by peer.")
                    {
                        _logger.LogError("{message} Connection reset by peer.", message);
                    }
                    else
                    {
                        _logger.LogError("{message} HttpRequestException.IOException: \"{ioMessage}\"", message, io.Message);
                    }
                }
                else
                {
                    _logger.LogError(re, "{message}", message);
                }
            }
            else
            {
                _logger.LogError(e, "{message}", message);
            }
        }
        #endregion
    }
}