using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchLib.Api.Helix.Models.Videos.GetVideos;
using TwitchVor.Conversion;
using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.Space;
using TwitchVor.Twitch.Chat;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Upload;
using TwitchVor.Utility;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Dota;
using TwitchVor.Vvideo.Money;
using TwitchVor.Vvideo.Pubg;
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
                List<BaseTimestamp> timestamps = streamHandler.timestamper.timestamps.ToList();
                SkipDb[] skips = await db.LoadSkipsAsync();

                TimeSpan totalLoss = TimeSpan.FromTicks(skips.Sum(s => (s.EndDate - s.StartDate).Ticks));

                List<Bill> bills = new();
                if (Program.config.Money is MoneyConfig moneyConfig)
                {
                    TimeBasedPricer appPricer = new(streamHandler.handlerCreationDate,
                        new Bill(moneyConfig.Currency, moneyConfig.PerHourCost));
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
                    bool hadDota = streamHandler.timestamper.timestamps.OfType<GameTimestamp>().Any(t =>
                        t.gameName?.Equals("Dota 2", StringComparison.OrdinalIgnoreCase) == true);

                    if (hadDota)
                    {
                        // TODO Если добавлю восстановление из руин, нужно будет также ограничивать и ДО.
                        // TODO Возможно, стоит поискать время начало стрима, а не создания хендлера.
                        try
                        {
                            dotaMatches =
                                await Program.dota.LoadMatchesAsync(afterTime: streamHandler.handlerCreationDate);
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
                        var matchesStamps = dotaMatches.Select(match => MakeDotaStamp(match,
                            Program.dota.config.TargetSteamId, heroes, Program.dota.config.SpoilResults)).ToArray();

                        timestamps.AddRange(matchesStamps);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Не удалось сформировать описание матчей доты.");
                    }
                }

                if (Program.pubg != null)
                {
                    bool hadPubg = streamHandler.timestamper.timestamps.OfType<GameTimestamp>().Any(t =>
                        t.gameId.Equals("493057", StringComparison.OrdinalIgnoreCase) == true);

                    if (hadPubg)
                    {
                        try
                        {
                            List<PubgMatch> matches =
                                await Program.pubg.GetMatchesAsync(streamHandler.handlerCreationDate);

                            PubgMatchTimestamp[] matchesStamps = matches.Select(MakePubgStamp).ToArray();

                            timestamps.AddRange(matchesStamps);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Не удалось сформировать описание пабга.");
                        }
                    }
                }

                Clip[] clips = streamHandler.chatWorker.CloneFetchedClips();
                if (clips.Length > 0)
                {
                    try
                    {
                        GetClipsResponse clipsResponse =
                            await Program.twitchAPI.Helix.Clips.GetClipsAsync(
                                clipIds: clips.Select(c => c.Id).ToList());

                        clips = clips.Select(clip =>
                        {
                            Clip? updatedClip = clipsResponse.Clips.FirstOrDefault(c => c.Id == clip.Id);

                            if (updatedClip == null)
                            {
                                _logger.LogWarning("Клип {id} не найден в обновлённом списке.", clip.Id);
                            }

                            return updatedClip ?? clip;
                        }).ToArray();

                        List<string> videoIds = clips.Select(clip => clip.VideoId)
                            .Distinct()
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();

                        if (videoIds.Count > 0)
                        {
                            GetVideosResponse vids =
                                await Program.twitchAPI.Helix.Videos.GetVideosAsync(videoIds: videoIds);

                            // Бредик
                            DateTime EstimateClipLocation(Clip clip, DateTime created, ChatClipConfig config)
                            {
                                return created - TimeSpan.FromSeconds(clip.Duration) + config.ClipOffset;
                            }

                            List<ChatClipTimestamp> clipStamps = new();
                            foreach (Clip clip in clips)
                            {
                                // этот код выглядит всрато, но я не буду его переписывать.
                                Video? video = vids.Videos.FirstOrDefault(v => v.Id == clip.VideoId);

                                DateTime clipDate;
                                if (video != null)
                                {
                                    if (clip.VodOffset != 0)
                                    {
                                        DateTime videoCreatedAt = DateTime.Parse(video.CreatedAt);

                                        clipDate = videoCreatedAt + TimeSpan.FromSeconds(clip.VodOffset);

                                        clipStamps.Add(new ChatClipTimestamp(clip.CreatorName, clip.CreatorId,
                                            clip.Title, clip.Url, clipDate));
                                        continue;
                                    }

                                    _logger.LogWarning("Для клипа нет оффсета {clip}", clip.Id);
                                }
                                else
                                {
                                    _logger.LogWarning("Не удалось найти видео для клипа {clip} ({id})", clip.Id,
                                        clip.VideoId);
                                }

                                DateTime clipCreatedAt = DateTime.Parse(clip.CreatedAt);

                                clipDate = EstimateClipLocation(clip, clipCreatedAt, Program.config.Chat?.FetchClips);

                                clipStamps.Add(new ChatClipTimestamp(clip.CreatorName, clip.CreatorId,
                                    clip.Title, clip.Url, clipDate));
                            }

                            timestamps.AddRange(clipStamps);
                        }
                        else
                        {
                            _logger.LogWarning("Нет видео для клипов.");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Не удалось проанализировать твич воды.");
                    }
                }

                var resultTimestamps = timestamps.OrderBy(t => t.GetTimeWithOffset()).ToArray();

                processingHandler = new(streamHandler.handlerCreationDate, streamHandler.db,
                    streamHandler.streamDownloader.AdvertismentTime, totalLoss, bills.ToArray(), resultTimestamps,
                    skips, subgifters, dotaMatches);
            }

            List<BaseUploader> uploaders;
            if (Program.config.Manual)
            {
                uploaders = DependencyProvider.GetAllUploaders(streamHandler.guid, _loggerFactory);

                if (uploaders.Count > 0)
                {
                    Console.WriteLine("Цифры через пробел, что выбираешь.");
                    for (int i = 0; i < uploaders.Count; i++)
                    {
                        Console.WriteLine($"[{i + 1}] {uploaders[i].GetType().Name}");
                    }

                    string read = Console.ReadLine();

                    BaseUploader[] selected = read.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .Select(index => uploaders[index - 1])
                        .Distinct()
                        .ToArray();

                    int counter = 0;
                    while (counter < uploaders.Count)
                    {
                        BaseUploader item = uploaders[counter];

                        if (selected.Contains(item))
                        {
                            counter++;
                        }
                        else
                        {
                            uploaders.RemoveAt(counter);
                        }
                    }
                }
            }
            else
            {
                uploaders = DependencyProvider.GetUploaders(streamHandler.guid, _loggerFactory);
            }

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
            bool
                destroyDB =
                    destroyVideo; // DependencyProvider.GetUploader(streamHandler.guid, _loggerFactory) is not Upload.FileSystem.FileUploader;

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
            List<ProcessingVideo> videos =
                await CutToVideosAsync(processingHandler.skips, uploader.SizeLimit, uploader.DurationLimit);

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

        async Task<List<ProcessingVideo>> CutToVideosAsync(IEnumerable<SkipDb> skips, long sizeLimit,
            TimeSpan durationLimit)
        {
            Queue<VideoFormatDb> formats = new(await db.LoadAllVideoFormatsAsync());

            VideoFormatDb currentFormat = formats.Dequeue();

            formats.TryDequeue(out VideoFormatDb? nextFormat);

            List<ProcessingVideo> videos = new();
            int tookCount = 0;
            int videoNumber = 0;
            // Один цикл = 1 видео
            while (true)
            {
                SegmentDb? startSegment = null;
                SegmentDb? endSegment = null;

                int startTookIndex = tookCount;
                int currentVideoTookCount = 0;

                long currentSize = 0;
                TimeSpan currentDuration = TimeSpan.Zero;
                // лупаем, пока не определим старт и енд сегменты видео
                bool keepGoing = true;
                while (keepGoing)
                {
                    SegmentDb[] segments = await db.LoadSegmentsAsync(takeCount, tookCount);

                    if (segments.Length == 0)
                    {
                        keepGoing = false;
                        continue; // не break ради смеха
                    }

                    foreach (SegmentDb segment in segments)
                    {
                        TimeSpan duration = TimeSpan.FromSeconds(segment.Duration);

                        if (currentSize + segment.Size > sizeLimit ||
                            currentDuration + duration > durationLimit)
                        {
                            // пора резать
                            keepGoing = false;
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
                                keepGoing = false;
                                break;
                            }
                        }

                        if (startSegment == null)
                        {
                            startSegment = segment;
                        }

                        endSegment = segment;

                        tookCount++;
                        currentVideoTookCount++;

                        currentSize += segment.Size;
                        currentDuration += duration;
                    }
                }

                if (currentVideoTookCount != 0)
                {
                    DateTimeOffset startDate = startSegment!.ProgramDate;
                    DateTimeOffset endDate = endSegment!.ProgramDate.AddSeconds(endSegment.Duration);

                    var relatedSkips = skips.Where(skip => skip.EndDate > startDate && skip.StartDate < endDate)
                        .ToArray();

                    TimeSpan loss = TimeSpan.FromTicks(relatedSkips.Sum(s =>
                    {
                        DateTimeOffset start = s.StartDate > startDate ? s.StartDate : startDate;
                        DateTimeOffset end = s.EndDate < endDate ? s.EndDate : endDate;

                        return (end - start).Ticks;
                    }));

                    videos.Add(new ProcessingVideo(videoNumber, startTookIndex, currentVideoTookCount, currentSize, startDate,
                        endDate, loss));

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
            _logger.LogInformation("Новый видос ({number}). {videoTook} сегментов, старт {startIndex}", video.number,
                video.segmentsCount, video.segmentStart);

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

                var cachedInfo = uploaderHandler.processingHandler.videoSizeCaches.FirstOrDefault(c =>
                    c.startSegmentId == video.segmentStart &&
                    c.endSegmentId == (video.segmentStart + video.segmentsCount));

                if (cachedInfo != null)
                {
                    _logger.LogDebug("Нашли в кеше.");

                    resultVideoSize = cachedInfo.size;
                }
                else
                {
                    _logger.LogDebug("Не нашли в кеше.");

                    if (uploaderHandler.uploader.NeedsExactVideoSize)
                    {
                        resultVideoSize = await CalculateResultVideoSizeAsync(video);

                        cachedInfo = new(video.segmentStart, video.segmentStart + video.segmentsCount, resultVideoSize);
                        uploaderHandler.processingHandler.videoSizeCaches.Add(cachedInfo);
                    }
                    else
                    {
                        _logger.LogDebug("Да и пофиг.");

                        resultVideoSize = video.size;
                    }
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
                                _logger.LogDebug("Финальная строка: {line}", lastConversionLine);
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
            string videoName = DescriptionMaker.FormVideoName(streamHandler.handlerCreationDate,
                singleVideo ? null : video.number, 100, uploaderHandler.processingHandler.timestamps);
            string description = uploaderHandler.MakeVideoDescription(video);

            video.uploadStart = DateTime.UtcNow;

            bool success;
            try
            {
                success = await uploaderHandler.uploader.UploadAsync(uploaderHandler, video, videoName, description,
                    filename, resultVideoSize, clientPipe);
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
                _ = Task.Run(() =>
                    PrintCountingWriteDataAsync(inputCountyStream, TimeSpan.FromSeconds(5), baseSize, _logger,
                        cts.Token));

                try
                {
                    await space.ReadAllDataAsync(inputCountyStream, size, offset, cts.Token);
                    await inputCountyStream.FlushAsync();

                    await inputStream.FlushAsync();
                    await inputStream.DisposeAsync();

                    await inputCountyStream.DisposeAsync();

                    _logger.LogInformation("Всё прочитали. {written} написано.", written);
                    return;
                }
                catch (Exception totalE)
                {
                    lastEx = totalE;

                    _logger.LogWarning(
                        "Перенаправление сегментов в ффмпег обернулось ошибкой. ({attempt}/{attemptsLimit}) ({bytes}) {message}",
                        attempt, attemptsLimit, inputCountyStream.TotalBytesWritten, totalE.Message);
                }
                finally
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch
                    {
                    }
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
                    } while (read > 0);

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
        static async Task PrintCountingWriteDataAsync(ByteCountingStream stream, TimeSpan cooldown, long size,
            ILogger logger, CancellationToken cancellationToken)
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
                catch
                {
                    return;
                }

                double writtenPerSec = (stream.TotalBytesWritten - writtenBefore) / cooldown.TotalSeconds;

                writtenBefore = stream.TotalBytesWritten;

                double secondsLeft = (size - stream.TotalBytesWritten) / writtenPerSec;

                await uline.UpdateAsync(
                    $"Написано {SomeUtis.MakeSizeFormat((long)writtenPerSec)}/сек Осталось {secondsLeft:F0} секунд ({SomeUtis.MakeSizeFormat(stream.TotalBytesWritten)}/{formattedSize})");
            }
        }

        static DotaMatchTimestamp MakeDotaStamp(Dota2Dispenser.Shared.Models.MatchModel match, ulong targetSteamId,
            IEnumerable<HeroModel> heroes, bool spoilResults)
        {
            var streamer = match.Players?.FirstOrDefault(p => p.SteamId == targetSteamId);

            if (streamer == null)
                return new DotaMatchTimestamp("???", 1, null, match.GameDate, spoilResults);

            string heroName = heroes.FirstOrDefault(hero => hero.Id == streamer.HeroId)?.LocalizedName ?? "Непонятно";

            bool? win;
            if (match.DetailsInfo?.RadiantWin != null && streamer.TeamNumber != null)
            {
                win = streamer.TeamNumber == 0
                    ? match.DetailsInfo.RadiantWin == true
                    : match.DetailsInfo.RadiantWin == false;
            }
            else win = null;

            int partyCount;
            if (streamer.PartyIndex != null)
                partyCount = match.Players!.Count(p => p.PartyIndex == streamer.PartyIndex);
            else
                partyCount = 1;

            return new DotaMatchTimestamp(heroName, partyCount, win, match.GameDate, spoilResults);
        }

        static PubgMatchTimestamp MakePubgStamp(PubgMatch match)
        {
            return new PubgMatchTimestamp(match);
        }
    }
}