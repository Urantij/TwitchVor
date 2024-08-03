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
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Dota;
using TwitchVor.Vvideo.Money;
using TwitchVor.Vvideo.Timestamps;

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
            ProcessingHandler processingHandler;
            {
                IEnumerable<BaseTimestamp> timestamps = streamHandler.timestamper.timestamps.ToArray();
                SkipDb[] skips = await db.LoadSkipsAsync();

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

                Dota2Dispenser.Shared.Models.MatchModel[]? dotaMatches = null;
                if (Program.dota != null)
                {
                    // TODO Можно ещё смотреть, чтобы стамп было дольше 10 минут.
                    // Мало ли с прошлого стрима остался.
                    bool hadDota = streamHandler.timestamper.timestamps.OfType<GameTimestamp>().Any(t => t.gameName?.Equals("Dota 2", StringComparison.OrdinalIgnoreCase) == true);

                    if (hadDota)
                    {
                        // TODO Если добавлю восстановление из руин, нужно будет также ограничивать и ДО.
                        // TODO Возможно, стоит поискать время начало стрима, а не создания хендлера.
                        try
                        {
                            dotaMatches = await Program.dota.LoadMatchesAsync(afterTime: streamHandler.handlerCreationDate);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Не удалось собрать матчи по доте.");
                        }
                    }
                }

                if (dotaMatches?.Length > 0)
                {
                    // В теории всё может сломаться, если пропадёт файл с героями.
                    // TODO Сделать ещё одну обёртку под матчи + героев и класть в процесс хендлер.
                    try
                    {
                        var heroes = await Program.dota!.LoadHeroesAsync();
                        var matchesStamps = dotaMatches.Select(match => MakeDotaStamp(match, Program.dota.config.TargetSteamId, heroes, Program.dota.config.SpoilResults)).ToArray();

                        timestamps = timestamps.Concat(matchesStamps);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Не удалось сформировать описание матчей доты.");
                    }
                }

                timestamps = timestamps.OrderBy(t => t.timestamp).ToArray();

                processingHandler = new(streamHandler.handlerCreationDate, streamHandler.db, streamHandler.streamDownloader.AdvertismentTime, totalLoss, bills.ToArray(), timestamps, skips, subgifters, dotaMatches);
            }

            var uploaders = DependencyProvider.GetUploaders(streamHandler.guid, _loggerFactory);

            bool allSuccess = true;
            foreach (var uploader in uploaders)
            {
                _logger.LogDebug("Работа аплоадера {type}", uploader.GetType().Name);

                bool success = await ProcessStreamAsync(processingHandler, uploader);

                if (allSuccess)
                {
                    allSuccess = success;
                }
            }

            processingHandler.SetResult();

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

        private async Task<bool> ProcessStreamAsync(ProcessingHandler processingHandler, BaseUploader uploader)
        {
            List<ProcessingVideo> videos = await CutToVideosAsync(processingHandler.skips, uploader.SizeLimit, uploader.DurationLimit);

            UploaderHandler uploaderHandler = new(uploader, processingHandler, videos);

            foreach (var video in uploaderHandler.videos)
            {
                video.processingStart = DateTimeOffset.UtcNow;
                try
                {
                    video.success = await DoVideoAsync(uploaderHandler, video);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Не удалось закончить загрузку видео.");

                    video.success = false;
                }
                video.processingEnd = DateTimeOffset.UtcNow;
            }

            uploaderHandler.SetResult();

            return uploaderHandler.videos.All(vid => vid.success == true);
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

        async Task<bool> DoVideoAsync(UploaderHandler uploaderHandler, ProcessingVideo video)
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

            long resultVideoSize;

            // Если конверсии нет, пишем сегменты в инпут (сервер пайп)
            // Если есть, пишем сегменты в инпут (инпут ффмпега), и пишем аутпут ффмпега в сервер пайп
            ConversionHandler? conversionHandler = null;
            string? lastConversionLine = null;
            bool hadFinalConverionLine = false;
            bool isFinalConverionLineLast = false;
            if (ffmpeg != null)
            {
                _logger.LogInformation("Используется конверсия, необходимо вычислить итоговый размер видео.");

                var cachedInfo = uploaderHandler.processingHandler.videoSizeCaches.FirstOrDefault(c => c.startSegmentId == video.segmentStart && c.endSegmentId == (video.segmentStart + video.segmentsCount));

                if (cachedInfo != null)
                {
                    _logger.LogDebug("Нашли в кеше.");

                    resultVideoSize = cachedInfo.size;
                }
                else
                {
                    _logger.LogDebug("Не нашли в кеше.");

                    resultVideoSize = await CalculateResultVideoSizeAsync(video);

                    cachedInfo = new(video.segmentStart, video.segmentStart + video.segmentsCount, resultVideoSize);
                    uploaderHandler.processingHandler.videoSizeCaches.Add(cachedInfo);
                }

                _logger.LogInformation("Итоговый размер видео {size} vs {oldSize}", resultVideoSize, video.size);

                conversionHandler = ffmpeg.CreateConversion();

                inputStream = conversionHandler.InputStream;

                // Читать ффмпег
                var readFfmpegTask = Task.Run(async () =>
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

                            if (Ffmpeg.CheckLastLine(lastConversionLine))
                            {
                                hadFinalConverionLine = true;
                                isFinalConverionLineLast = true;
                            }
                            else if (isFinalConverionLineLast)
                            {
                                isFinalConverionLineLast = false;
                            }
                        }

                        await uploaderHandler.ProcessTask;
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
                var redirectFfmpegTask = Task.Run(async () =>
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

                resultVideoSize = video.size;
            }

            // Чтение сегментов, перенаправление в инпут.
            var writeTask = Task.Run(() => WriteVideoAsync(video, inputStream));

            bool singleVideo = uploaderHandler.videos.Count == 1;
            string videoName = DescriptionMaker.FormVideoName(streamHandler.handlerCreationDate, singleVideo ? null : video.number, 100, uploaderHandler.processingHandler.timestamps);
            string description = uploaderHandler.MakeVideoDescription(video);

            video.uploadStart = DateTime.UtcNow;

            bool success;
            try
            {
                success = await uploaderHandler.uploader.UploadAsync(uploaderHandler, video, videoName, description, filename, resultVideoSize, clientPipe);
            }
            finally
            {
                video.uploadEnd = DateTime.UtcNow;
            }

            if (conversionHandler != null)
            {
                int conversionExitCode = await conversionHandler.WaitAsync();
                bool conversionSuccess = conversionExitCode == 0;

                conversionHandler.Dispose();

                if (conversionSuccess)
                {
                    _logger.LogDebug("Последняя строка ффмпега\n{text}", lastConversionLine);
                    conversionSuccess = hadFinalConverionLine;

                    if (hadFinalConverionLine && !isFinalConverionLineLast)
                    {
                        _logger.LogWarning("Итоговая строчка была, но не последней.");
                    }
                }

                if (!conversionSuccess)
                {
                    _logger.LogCritical("Конверсия не удалась. {code}. {line}", conversionExitCode, lastConversionLine);

                    success = false;
                }
            }

            // Некоторые аплоадеры не закрывают стрим.
            await serverPipe.DisposeAsync();

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
                _ = Task.Run(() => PrintCountingWriteDataAsync(inputCountyStream, TimeSpan.FromSeconds(5), baseSize, _logger, cts.Token));

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
            await errorTask;
            await readTask;

            conversionHandler.Dispose();

            return resultSize;
        }

        // TODO Как-то бы обозначить, что это не скорость загрузки, а скорость обработки.
        // Потому что обрабатывается больше байт, чем передаётся.
        // Ну да ладно.
        static async Task PrintCountingWriteDataAsync(ByteCountingStream stream, TimeSpan cooldown, long size, ILogger logger, CancellationToken cancellationToken)
        {
            long writtenBefore = stream.TotalBytesWritten;

            var uline = await UpdatableLine.Create("Загрузка...", cancellationToken);

            string formattedSize = SomeUtis.MakeSizeFormat(size);

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

                await uline.UpdateAsync($"Написано {SomeUtis.MakeSizeFormat((long)writtenPerSec)}/сек Осталось {secondsLeft:F0} секунд ({SomeUtis.MakeSizeFormat(stream.TotalBytesWritten)}/{formattedSize})");
            }
        }

        static DotaMatchTimestamp MakeDotaStamp(Dota2Dispenser.Shared.Models.MatchModel match, ulong targetSteamId, IEnumerable<HeroModel> heroes, bool spoilResults)
        {
            var streamer = match.Players?.FirstOrDefault(p => p.SteamId == targetSteamId);

            if (streamer == null)
                return new DotaMatchTimestamp("???", 1, null, match.GameDate, spoilResults);

            string heroName = heroes.FirstOrDefault(hero => hero.Id == streamer.HeroId)?.LocalizedName ?? "Непонятно";

            bool? win;
            if (match.DetailsInfo?.RadiantWin != null && streamer.TeamNumber != null)
            {
                win = streamer.TeamNumber == 0 ? match.DetailsInfo.RadiantWin == true : match.DetailsInfo.RadiantWin == false;
            }
            else win = null;

            int partyCount;
            if (streamer.PartyIndex != null)
                partyCount = match.Players!.Count(p => p.PartyIndex == streamer.PartyIndex);
            else
                partyCount = 1;

            return new DotaMatchTimestamp(heroName, partyCount, win, match.GameDate, spoilResults);
        }
    }
}