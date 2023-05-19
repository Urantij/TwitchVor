using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Conversion;
using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.EventIds;
using TwitchVor.Space;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Upload;
using TwitchVor.Utility;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Finisher
{
    class StreamFinisher
    {
        const int takeCount = 250;

        readonly ILogger _logger;

        readonly Ffmpeg? ffmpeg;

        readonly StreamHandler streamHandler;
        readonly ILoggerFactory _loggerFactory;

        readonly StreamDatabase db;
        readonly BaseSpaceProvider space;

        public StreamFinisher(StreamHandler streamHandler, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            ffmpeg = Program.ffmpeg;

            this.streamHandler = streamHandler;
            _loggerFactory = loggerFactory;

            db = streamHandler.db;
            space = streamHandler.space;
        }

        public async Task DoAsync()
        {
            var uploaders = DependencyProvider.GetUploaders(streamHandler.guid, _loggerFactory);

            bool allSuccess = true;
            foreach (var uploader in uploaders)
            {
                _logger.LogDebug("Работа аплоадера {type}", uploader.GetType().Name);
                var processingHandler = await ProcessStreamAsync(uploader);

                if (allSuccess)
                {
                    allSuccess = processingHandler.videos.All(v => v.success == true);
                }
            }

            if (allSuccess)
            {
                _logger.LogInformation("С видосами закончили, всё нормально.");
            }
            else
            {
                _logger.LogInformation("С видосами закончили, всё хуёво.");
            }

            // костыль мне плохо
            bool destroyVideo = allSuccess;
            bool destroyDB = destroyVideo; // DependencyProvider.GetUploader(streamHandler.guid, _loggerFactory) is not Upload.FileSystem.FileUploader;

            if (Program.config.SaveTheVideo)
            {
                destroyVideo = false;
                destroyDB = false;
            }

            await streamHandler.DestroyAsync(destroySegments: destroyVideo, destroyDB: destroyDB);

            if (Program.emailer != null)
            {
                _logger.LogInformation("Отправляем весточку.");

                if (allSuccess)
                {
                    await Program.emailer.SendFinishSuccessAsync();
                }
                else
                {
                    await Program.emailer.SendCriticalErrorAsync("Не получилось нормально закончить стрим...");
                }
            }

            if (Program.shutdown)
            {
                _logger.LogInformation("Shutdown...");
                using Process pr = new();

                pr.StartInfo.FileName = "shutdown";

                pr.StartInfo.UseShellExecute = false;
                pr.StartInfo.RedirectStandardOutput = true;
                pr.StartInfo.RedirectStandardError = true;
                pr.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pr.StartInfo.CreateNoWindow = true;
                pr.Start();

                pr.OutputDataReceived += (s, e) => { _logger.LogInformation(e.Data); };
                pr.ErrorDataReceived += (s, e) => { _logger.LogInformation(e.Data); };

                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                pr.WaitForExit();
            }
        }

        private async Task<ProcessingHandler> ProcessStreamAsync(BaseUploader uploader)
        {
            ProcessingHandler processingHandler;
            {
                var skips = await db.LoadSkipsAsync();

                var videos = await CutToVideosAsync(skips, uploader.SizeLimit, uploader.DurationLimit);

                TimeSpan totalLoss = TimeSpan.FromTicks(skips.Sum(s => (s.EndDate - s.StartDate).Ticks));

                List<Bill> bills = new();
                if (Program.config.Money is MoneyConfig moneyConfig)
                {
                    TimeBasedPricer appPricer = new(streamHandler.handlerCreationDate, new Bill(moneyConfig.Currency, moneyConfig.PerHourCost));
                    bills.Add(appPricer.GetCost(DateTimeOffset.UtcNow));
                }
                if (streamHandler.space.pricer != null)
                    bills.Add(streamHandler.space.pricer.GetCost(DateTimeOffset.UtcNow));

                string[] subgifters = await DescriptionMaker.GetDisplaySubgiftersAsync(streamHandler.subCheck);

                processingHandler = new(streamHandler.handlerCreationDate, streamHandler.db, streamHandler.streamDownloader.AdvertismentTime, totalLoss, bills.ToArray(), streamHandler.timestamper.timestamps, skips, videos.ToArray(), subgifters);
            }

            foreach (var video in processingHandler.videos)
            {
                video.uploadStart = DateTimeOffset.UtcNow;
                try
                {
                    video.success = await DoVideoAsync(processingHandler, video, uploader, singleVideo: processingHandler.videos.Length == 1);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Не удалось закончить загрузку видео.");

                    video.success = false;
                }
                video.uploadEnd = DateTimeOffset.UtcNow;
            }

            processingHandler.SetResult();

            return processingHandler;
        }

        async Task<List<ProcessingVideo>> CutToVideosAsync(IEnumerable<SkipDb> skips, long sizeLimit, TimeSpan durationLimit)
        {
            Queue<VideoFormatDb> formats = new(await db.LoadAllVideoFormatsAsync());

            VideoFormatDb currentFormat = formats.Dequeue();

            formats.TryDequeue(out VideoFormatDb? nextFormat);

            List<ProcessingVideo> videos = new();
            int tookCount = 0;
            int videoNumber = 0;
            while (true)
            {
                SegmentDb? startSegment = null;
                SegmentDb? endSegment = null;

                int startTookIndex = tookCount;
                int videoTook = 0;

                long currentSize = 0;
                TimeSpan currentDuration = TimeSpan.Zero;
                while (true)
                {
                    var segments = await db.LoadSegmentsAsync(takeCount, tookCount);

                    if (segments.Length == 0)
                    {
                        break;
                    }
                    else
                    {
                        foreach (var segment in segments)
                        {
                            TimeSpan duration = TimeSpan.FromSeconds(segment.Duration);

                            if (currentSize + segment.Size > sizeLimit ||
                                currentDuration + duration > durationLimit)
                            {
                                // пора резать
                                break;
                            }

                            if (nextFormat != null && segment.ProgramDate >= nextFormat.Date)
                            {
                                // Он не должен записывать формат, если он тот же, но я мб передумаю в будущем.
                                bool changed = nextFormat.Format != currentFormat.Format;

                                currentFormat = nextFormat;
                                formats.TryDequeue(out nextFormat);

                                if (changed)
                                {
                                    // Сменился формат - нужно новое видео, туда же писать уже нельзя.

                                    break;
                                }
                            }

                            if (startSegment == null)
                            {
                                startSegment = segment;
                            }

                            endSegment = segment;

                            tookCount++;
                            videoTook++;

                            currentSize += segment.Size;
                            currentDuration += duration;
                        }
                    }
                }

                if (videoTook != 0)
                {
                    DateTimeOffset startDate = startSegment!.ProgramDate;
                    DateTimeOffset endDate = endSegment!.ProgramDate.AddSeconds(endSegment.Duration);

                    var relatedSkips = skips.Where(skip => skip.EndDate > startDate && skip.StartDate < endDate).ToArray();

                    TimeSpan loss = TimeSpan.FromTicks(relatedSkips.Sum(s =>
                    {
                        DateTimeOffset start = s.StartDate > startDate ? s.StartDate : startDate;
                        DateTimeOffset end = s.EndDate < endDate ? s.EndDate : endDate;

                        return (end - start).Ticks;
                    }));

                    videos.Add(new ProcessingVideo(videoNumber, startTookIndex, videoTook, currentSize, startDate, endDate, loss));

                    videoNumber++;
                }
                else
                {
                    break;
                }
            }

            return videos;
        }

        async Task<bool> DoVideoAsync(ProcessingHandler processingHandler, ProcessingVideo video, BaseUploader uploader, bool singleVideo)
        {
            _logger.LogInformation("Новый видос ({number}). {videoTook} сегментов, старт {startIndex}", video.number, video.segmentsCount, video.segmentStart);

            int limitIndex = video.segmentStart + video.segmentsCount;

            string filename = $"result{video.number}." + (ffmpeg != null ? "mp4" : "ts");

            // Сюда пишем то, что будет читать аплоадер.
            // Когда закончим писать, его нужно будет закрыть.
            using var serverPipe = new AnonymousPipeServerStream(PipeDirection.Out);
            // Это читает аплоадер.
            using var clientPipe = new AnonymousPipeClientStream(PipeDirection.In, serverPipe.ClientSafePipeHandle);

            // Сюда пишем то, что вычитали из сегментов.
            Stream inputStream;

            long videoSize;

            // Если конверсии нет, пишем сегменты в инпут (сервер пайп)
            // Если есть, пишем сегменты в инпут (инпут ффмпега), и пишем аутпут ффмпега в сервер пайп
            ConversionHandler? conversionHandler = null;
            string? lastConversionLine = null;
            if (ffmpeg != null)
            {
                _logger.LogInformation("Используется конверсия, необходимо вычислить итоговый размер видео.");
                videoSize = await CalculateResultVideoSizeAsync(video);

                _logger.LogInformation("Итоговый размер видео {size} vs {oldSize}", videoSize, video.size);

                conversionHandler = ffmpeg.CreateConversion();

                inputStream = conversionHandler.InputStream;

                // Читать ффмпег
                _ = Task.Run(async () =>
                {
                    string logPath = DependencyProvider.MakePath(streamHandler.guid.ToString("N") + ".ffmpeg.log");
                    FileStream fs = new(logPath, FileMode.Create);

                    try
                    {
                        while (true)
                        {
                            var line = await conversionHandler.TextStream.ReadLineAsync();

                            if (line == null)
                            {
                                _logger.LogInformation("ффмпег закончил говорить.");
                                break;
                            }

                            lastConversionLine = line;

                            await fs.WriteAsync(System.Text.Encoding.UTF8.GetBytes(line + '\n'));
                        }

                        await processingHandler.ProcessTask;
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "Чтение строчек ффмпега обернулось ошибкой.");
                    }
                    finally
                    {
                        await fs.DisposeAsync();

                        if (video.success == true)
                        {
                            File.Delete(logPath);
                        }
                    }
                });

                // Перенаправление выхода ффмпега в сервер.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await conversionHandler.OutputStream.CopyToAsync(serverPipe);
                        await conversionHandler.OutputStream.FlushAsync();

                        await serverPipe.DisposeAsync();

                        _logger.LogInformation("Закончился выход у ффмпега.");
                    }
                    catch (Exception e)
                    {
                        _logger.LogCritical(e, "Перенаправление ффмпега обернулось ошибкой.");
                    }
                });
            }
            else
            {
                inputStream = serverPipe;

                videoSize = video.size;
            }

            // Чтение сегментов, перенаправление в инпут.
            _ = Task.Run(() => WriteVideoAsync(video, inputStream));

            string videoName = DescriptionMaker.FormVideoName(streamHandler.handlerCreationDate, singleVideo ? null : video.number, 100, processingHandler.timestamps);

            string description = processingHandler.MakeVideoDescription(video);

            bool success = await uploader.UploadAsync(processingHandler, video, videoName, description, filename, videoSize, clientPipe);

            if (conversionHandler != null)
            {
                bool conversionSuccess = await conversionHandler.WaitAsync();

                conversionHandler.Dispose();

                if (conversionSuccess)
                {
                    _logger.LogDebug("Последняя строка ффмпега\n{text}", lastConversionLine);
                    conversionSuccess = Ffmpeg.CheckLastLine(lastConversionLine);
                }

                if (!conversionSuccess)
                {
                    _logger.LogCritical("Конверсия не удалась. {code}. {line}", conversionHandler.ExitCode, lastConversionLine);

                    success = false;
                }
            }

            _logger.LogInformation("Видос всё.");

            return success;
        }

        async Task WriteVideoAsync(ProcessingVideo video, Stream inputStream)
        {
            long baseOffset = await db.CalculateOffsetAsync(video.segmentStart);
            long baseSize = await db.CalculateSizeAsync(video.segmentStart, video.segmentStart + video.segmentsCount);

            _logger.LogDebug("Примерный размер видео {size}", baseSize);

            long written = 0;

            const int attemptsLimit = 5;
            int attempt = 1;
            int attemptsLeft = attemptsLimit;

            Exception lastEx = new();
            while (attemptsLeft > 0)
            {
                int clientAttempt = attempt;

                long offset = baseOffset + written;
                long size = baseSize - written;

                _logger.LogDebug("Попытка {attempt}, осталось {size}", clientAttempt, size);

                if (size == 0)
                {
                    _logger.LogWarning("Size 0");

                    await inputStream.FlushAsync();
                    await inputStream.DisposeAsync();
                    break;
                }

                using var cts = new CancellationTokenSource();

                ByteCountingStream inputCountyStream = new(inputStream);
                _ = Task.Run(() => PrintCountingWriteDataAsync(inputCountyStream, TimeSpan.FromSeconds(20), baseSize, _logger, cts.Token));

                try
                {
                    await space.ReadAllDataAsync(inputCountyStream, size, offset, cts.Token);
                    await inputCountyStream.FlushAsync();

                    await inputStream.FlushAsync();
                    await inputStream.DisposeAsync();

                    await inputCountyStream.DisposeAsync();

                    _logger.LogInformation("Всё прочитали.");
                    return;
                }
                catch (Exception totalE)
                {
                    lastEx = totalE;

                    _logger.LogWarning("Перенаправление сегментов в ффмпег обернулось ошибкой. ({attempt}/{attemptsLimit}) ({bytes}) {message}", attempt, attemptsLimit, inputCountyStream.TotalBytesWritten, totalE.Message);
                }
                finally
                {
                    try { cts.Cancel(); } catch { }
                }

                if (inputCountyStream.TotalBytesWritten > 0)
                {
                    written += inputCountyStream.TotalBytesWritten;
                    attemptsLeft = attemptsLimit;
                }
                else
                {
                    attemptsLeft--;
                }
                attempt++;
            }

            _logger.LogCritical(lastEx, "Перенаправление сегментов в ффмпег обернулось ошибкой.");

            await inputStream.DisposeAsync();
        }

        async Task<string?> ReadFfmpegTextAsync(StreamReader outputStream)
        {
            string? lastConversionLine = null;
            try
            {
                while (true)
                {
                    var line = await outputStream.ReadLineAsync();

                    if (line == null)
                    {
                        _logger.LogInformation("ффмпег закончил говорить.");
                        return lastConversionLine;
                    }

                    lastConversionLine = line;
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Чтение строчек ффмпега обернулось ошибкой.");
            }

            return null;
        }

        async Task<long> CalculateResultVideoSizeAsync(ProcessingVideo video)
        {
            if (ffmpeg == null)
                throw new Exception($"{nameof(ffmpeg)} is null");

            long resultSize = 0;

            var conversionHandler = ffmpeg.CreateConversion();

            var errorTask = Task.Run(() => ReadFfmpegTextAsync(conversionHandler.TextStream));

            var readTask = Task.Run(async () =>
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int read = 0;
                    do
                    {
                        read = await conversionHandler.OutputStream.ReadAsync(buffer);

                        resultSize += read;
                    }
                    while (read > 0);

                    _logger.LogInformation("Закончился выход у ффмпега.");
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Перенаправление ффмпега обернулось ошибкой.");
                }
            });

            await WriteVideoAsync(video, conversionHandler.InputStream);

            // TODO Можно из последней строки размер получать и сравнивать.
            // await errorTask;
            // await readTask;

            conversionHandler.Dispose();

            return resultSize;
        }

        // TODO Как-то бы обозначить, что это не скорость загрузки, а скорость обработки.
        // Потому что обрабатывается больше байт, чем передаётся.
        // Ну да ладно.
        static async Task PrintCountingWriteDataAsync(ByteCountingStream stream, TimeSpan cooldown, long size, ILogger logger, CancellationToken cancellationToken)
        {
            long writtenBefore = stream.TotalBytesWritten;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(cooldown, cancellationToken);
                }
                catch { return; }

                double writtenPerSec = (stream.TotalBytesWritten - writtenBefore) / cooldown.TotalSeconds;

                writtenBefore = stream.TotalBytesWritten;

                double secondsLeft = (size - stream.TotalBytesWritten) / writtenPerSec;

                logger.LogInformation("Написано {totalWritten} {perSec:F0}/сек Осталось {seconds:F0} секунд", stream.TotalBytesWritten, writtenPerSec, secondsLeft);
            }
        }
    }
}