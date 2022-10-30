using System;
using System.Diagnostics;
using System.Text;
using TwitchVor.Configuration;
using TwitchVor.Ocean;
using TwitchVor.Twitch;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Upload.Kvk;
using TwitchVor.Upload.TubeYou;
using TwitchVor.Utility;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Timestamps;

namespace TwitchVor.Finisher
{
    class StreamFinisher
    {
        readonly StreamHandler stream;

        public StreamFinisher(StreamHandler stream)
        {
            this.stream = stream;
        }

        void Log(string message)
        {
            //TODO айди
            ColorLog.Log(message, "Finisher");
        }

        public async Task FinishAsync()
        {
            /* Бот может быть локальным, может быть на облаке
             * Может быть с конверсией, может быть без
             * Может быть с загрузкой на ютуб, может быть без 
             *
             * И в зависимости от этих переменных, алгоритм будет разный 
             *
             * Облачный всегда с загрузкой на ютуб. И если есть конверсия, он удалит оригинал только после загрузки
             * Тому шо оставлять конвертированную копию на втором вольюме не вариант
             * Либо удалять оригинал, но в случае неуспеха копировать конвертированный видос обратно. Звучит неплохо.
             *
             * А локальному похуй. */

            DigitalOceanVolumeOperator? secondVolumeOperator = null;
            List<VideoSummary> summaries = new();
            bool summarySuccess = true;

            Task? finishingTask = null;

            if (stream.currentVideoWriter == null)
            {
                Log("Охуенный стрим без видео.");
                goto end;
            }

            var subgifters = await GetDisplaySubgiftersAsync();

            List<VideoWriter> videoWriters = new();
            videoWriters.AddRange(stream.pastVideoWriters);
            videoWriters.Add(stream.currentVideoWriter);

            // if (Program.config.YouTube != null && Program.config.Ocean != null && Program.config.Conversion != null)
            if (Program.config.Vk != null && Program.config.Ocean != null && Program.config.Conversion != null)
            {
                var maxSizeBytes = videoWriters.Select(w => new FileInfo(w.linkedThing.FilePath).Length).Max();

                //Можете кибербулить меня, но я хуй знает, че там у до на уме.
                //var maxSizeGB = (int)(maxSizeBytes / 1024d / 1024d / 1024d * 1.1d);
                int maxSizeGB = (int)Math.Ceiling(maxSizeBytes / 1000M / 1000M / 1000M * 1.1M);

                if (maxSizeBytes < 10)
                {
                    Log($"Вольюм слишком мал {maxSizeBytes}, устанавливаем размер 10GB");
                    maxSizeBytes = 10;
                }

                secondVolumeOperator = await CreateSecondVolumeAsync(maxSizeGB);
            }

            TimeSpan? totalConversionTime = null;
            TimeSpan? totalUploadTime = null;

            for (int videoIndex = 0; videoIndex < videoWriters.Count; videoIndex++)
            {
                var video = videoWriters[videoIndex];

                if (video.linkedThing.estimatedSize == 0 || video.linkedThing.estimatedDuration == 0 || video.linkedThing.firstSegmentDate == null)
                {
                    Log($"Охуенное видео без сегментов. {videoIndex}");
                    continue;
                }

                string origPath = video.linkedThing.FilePath;
                bool saveDescriptionLocally = true;

                int totalLost = (int)videoWriters.Sum(video => video.skipInfos.Sum(skip => (skip.whenEnded - skip.whenStarted).TotalSeconds));
                int advertLost = (int)stream.advertismentSeconds;

                bool converted = false;
                TimeSpan? conversionTime = null;
                if (Program.config.Conversion is ConversionConfig)
                {
                    string targetFilePath = secondVolumeOperator != null ? $"/mnt/{secondVolumeOperator.volumeName}/{video.linkedThing.FileName}" : video.linkedThing.FilePath;

                    (converted, conversionTime) = ConvertVideo(video, targetFilePath);

                    if (converted)
                    {
                        totalConversionTime = totalConversionTime != null ? (totalConversionTime + conversionTime) : conversionTime;
                    }
                }

                string videoName = FormName(stream.handlerCreationDate, videoWriters.Count == 1 ? null : videoIndex + 1);
                string videoDescription = FormDescription(video, subgifters, totalLost, advertLost, conversionTime, null, null, null, null, null);

                bool uploaded = false;
                TimeSpan? uploadTime = null;
                string? videoId = null;
                // if (Program.config.YouTube != null)
                if (Program.config.Vk != null)
                {
                    (uploaded, videoId, uploadTime) = await UploadVideoAsync(video, videoName, videoDescription);

                    if (!uploaded)
                    {
                        summarySuccess = false;
                    }

                    if (uploaded)
                    {
                        totalUploadTime = totalUploadTime != null ? (totalUploadTime + uploadTime) : uploadTime;

                        saveDescriptionLocally = false;
                    }
                    else if (converted && secondVolumeOperator != null)
                    {
                        // если не удалось загрузить на облаке, и оно было конвертировано, нужно вернуть файл на основной вольюм
                        string returnPath = Path.ChangeExtension(origPath, Program.config.Conversion!.TargetFormat);

                        File.Move(video.linkedThing.FilePath, returnPath);

                        Log("Вернули конвертированный файл на основной вольюм");
                    }
                }

                VideoSummary summary = new(video)
                {
                    videoId = videoId,

                    uploaded = uploaded,

                    conversionTime = conversionTime,
                    uploadTime = uploadTime
                };
                summaries.Add(summary);

                if (saveDescriptionLocally)
                {
                    string fileName = Path.ChangeExtension(video.linkedThing.FileName, "description");
                    string path = Path.Combine(Program.config.LocalDescriptionsDirectoryName, fileName);

                    string content = $"{videoName}\n\n{videoDescription}";

                    try
                    {
                        await File.WriteAllTextAsync(path, content);
                        Log("Описание видео записано в папку.");
                    }
                    catch (Exception e)
                    {
                        Log($"Не удалось записать описание видео, исключение:\n{e}");
                    }
                }
            }

        // if (Program.config.YouTube != null)
        // {
        //     finishingTask = ContinueVideoninining(summaries, subgifters, totalConversionTime, totalUploadTime);
        // }

        end:;

            //Если облачные технологии, в случае успеха нужно удалить все вольюмы. В случае неуспеха только второй.
            if (Program.config.Ocean != null)
            {
                List<DigitalOceanVolumeOperator> operators = new();

                if (summarySuccess && stream.volumeOperator2 != null)
                    operators.Add(stream.volumeOperator2);

                if (secondVolumeOperator != null)
                    operators.Add(secondVolumeOperator);

                if (operators.Count > 0)
                {
                    bool fine = await DestroyVolumes(operators);

                    if (!fine && Program.emailer != null)
                    {
                        await Program.emailer.SendCriticalErrorAsync("Не удалось сломать вольюмы");
                    }
                }
            }

            if (Program.emailer != null)
            {
                bool fine = true;

                // if (Program.config.YouTube != null)
                if (Program.config.Vk != null)
                {
                    fine = summarySuccess;
                }

                if (fine)
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
                Log("Shutdown...");
                var superfinish = () =>
                {
                    using Process pr = new();

                    pr.StartInfo.FileName = "shutdown";

                    pr.StartInfo.UseShellExecute = false;
                    pr.StartInfo.RedirectStandardOutput = true;
                    pr.StartInfo.RedirectStandardError = true;
                    pr.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    pr.StartInfo.CreateNoWindow = true;
                    pr.Start();

                    pr.OutputDataReceived += (s, e) => { Log(e.Data); };
                    pr.ErrorDataReceived += (s, e) => { Log(e.Data); };

                    pr.BeginOutputReadLine();
                    pr.BeginErrorReadLine();

                    pr.WaitForExit();
                };

                if (finishingTask != null)
                {
                    await finishingTask.ContinueWith(t => superfinish());
                }
                else superfinish();
            }
        }

        async Task<string[]> GetDisplaySubgiftersAsync()
        {
            List<SubCheck> tempList = new();

            if (stream.subCheck != null)
                tempList.Add(stream.subCheck);

            if (Program.config.Downloader?.SubCheck != null)
            {
                var postStreamSubCheck = await SubChecker.GetSub(Program.config.ChannelId!, Program.config.Downloader.SubCheck.AppSecret, Program.config.Downloader.SubCheck.AppClientId, Program.config.Downloader.SubCheck.UserId, Program.config.Downloader.SubCheck.RefreshToken);

                if (postStreamSubCheck != null)
                    tempList.Add(postStreamSubCheck);
            }

            return tempList.Where(s => s.sub)
                           .Reverse() //ник мог поменяться, тогда нужно юзать самый новый
                           .DistinctBy(sc => sc.subInfo?.GiftertId)
                           .Select(sc =>
                           {
                               if (sc.subInfo == null)
                                   return "???";

                               if (sc.subInfo.GifterName.Equals(sc.subInfo.GifterLogin, StringComparison.OrdinalIgnoreCase))
                               {
                                   return sc.subInfo.GifterName;
                               }

                               return $"{sc.subInfo.GifterName} ({sc.subInfo.GifterLogin})";

                           }).ToArray();
        }

        async Task<DigitalOceanVolumeOperator> CreateSecondVolumeAsync(int size)
        {
            Log($"Создаём второй вольюм, размер ({size})...");

            string volumeName = DigitalOceanVolumeCreator.GenerateVolumeName(DateTime.UtcNow);

            DigitalOceanVolumeCreator creator = new(Program.config.Ocean!, volumeName, size);

            var secondVolumeOperator = await creator.CreateAsync();

            //никак не нулл
            stream.pricer!.AddVolume(DateTime.UtcNow, size);
            Log($"Создан второй вольюм.");

            await Task.Delay(TimeSpan.FromSeconds(5));

            return secondVolumeOperator;
        }

        /// <summary>
        /// Конвертирует видео, создавая новую копию в указанном месте. В случае успеха удаляет оригинал
        /// </summary>
        /// <param name="video"></param>
        /// <param name="targetFilePath">Можно с форматом, можно без. Без разницы.</param>
        /// <param name="deleteOrigOnSuccess"></param>
        /// <returns></returns>
        (bool converted, TimeSpan passed) ConvertVideo(VideoWriter video, string targetFilePath)
        {
            Log($"Конвертируем {video.linkedThing.FileName}...");

            string oldPath = video.linkedThing.FilePath;

            string newName = Path.ChangeExtension(video.linkedThing.FileName, Program.config.Conversion!.TargetFormat);
            string newPath = Path.ChangeExtension(targetFilePath, Program.config.Conversion!.TargetFormat);

            DateTime startTime = DateTime.UtcNow;

            bool converted = Ffmpeg.Convert(oldPath, newPath);

            var passed = DateTime.UtcNow - startTime;

            if (converted)
            {
                Log($"Конвертировано. Заняло {passed.TotalMinutes} минут.");

                try
                {
                    File.Delete(oldPath);
                    Log("Старый файл удалён.");
                }
                catch (Exception e)
                {
                    Log($"Не удалось удалить старый файл, исключение:\n{e}");
                }

                video.linkedThing.SetPath(newPath);
                video.linkedThing.SetName(newName);
            }
            else
            {
                Log($"Не удалось конвертировать. Заняло {passed.TotalMinutes} минут.");

                try { File.Delete(newPath); }
                catch { }
            }

            return (converted, passed);
        }

        /// <summary>
        /// Загружает видево на ютуб. В случае успеха удаляет локальную копию видева
        /// </summary>
        /// <param name="video"></param>
        /// <param name="videoName"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        async Task<(bool uploaded, string? videoId, TimeSpan passed)> UploadVideoAsync(VideoWriter video, string videoName, string description)
        {
            // YoutubeUploader uploader = new(Program.config.YouTube!);
            VkUploader uploader = new(Program.config.Vk!);

            TimeSpan uploadTime;

            Log($"Загружаем {video.linkedThing.FilePath}...");
            bool uploaded;
            using (var fileStream = new FileStream(video.linkedThing.FilePath, FileMode.Open))
            {
                var uploadStartDate = DateTime.UtcNow;
                uploaded = await uploader.UploadAsync(videoName, description, video.linkedThing.FileName, fileStream);
                // uploaded = await uploader.UploadAsync(videoName, description, Program.config.YouTube!.VideoTags, fileStream, "public");

                uploadTime = DateTime.UtcNow - uploadStartDate;
            }

            string? videoId = null;
            if (uploaded)
            {
                Log($"Загрузка успешно закончена {video.linkedThing.FilePath}. Заняло {uploadTime.TotalMinutes} минут.");
                try
                {
                    File.Delete(video.linkedThing.FilePath);
                    Log("Видео удалено с диска.");
                }
                catch (Exception e)
                {
                    Log($"Не удалось удалить файл, исключение:\n{e}");
                }

                // videoId = uploader.videoId;

                // if (uploader.videoId == null)
                // {
                //     Log($"{nameof(uploader.videoId)} is null");
                // }
            }
            else
            {
                Log($"Не удалось загрузить видео {video.linkedThing.FilePath}. Заняло {uploadTime.TotalMinutes} минут.");
            }

            return (uploaded, videoId, uploadTime);
        }

        async Task<bool> DestroyVolumes(List<DigitalOceanVolumeOperator> operators)
        {
            //расправа

            List<Task<bool>> tasks = new();
            foreach (var op in operators)
            {
                var task = Task.Run<bool>(async () =>
                {
                    bool fine = false;

                    int retries = 0;
                    while (retries < 3)
                    {
                        try
                        {
                            await op.DetachAsync();
                            break;
                        }
                        catch (Exception e)
                        {
                            Log($"Не удалось отсоединить вольюм {op.volumeName}. Исключение:\n{e}");

                            retries++;
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    retries = 0;
                    while (retries < 3)
                    {
                        try
                        {
                            await op.DeleteAsync();

                            fine = true;
                            break;
                        }
                        catch (Exception e)
                        {
                            Log($"Не удалось удалить вольюм {op.volumeName}. Исключение:\n{e}");

                            retries++;
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    //хз че будет, если не удалённому вольюму удалить папку, но мне похуй
                    try
                    {
                        Directory.Delete($"/mnt/{op.volumeName}");
                        Log($"Папка {op.volumeName} удалена");
                    }
                    catch (Exception e)
                    {
                        Log($"Не удалось удалить папку {op.volumeName}: {e.Message}");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));

                    return fine;
                });

                tasks.Add(task);

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            Task.WaitAll(tasks.ToArray());

            return tasks.All(t => t.Result);
        }

        // async Task ContinueVideoninining(List<VideoSummary> summaries2, string[] subgifters, TimeSpan? totalConversionTime, TimeSpan? totalUploadTime)
        // {
        //     var validSummaries = summaries2.Where(s => s.uploaded && s.videoId != null).ToList();

        //     if (validSummaries.Count == 0)
        //     {
        //         Log("Ты прикинь, ничего не загрузилось. Вот дела...");
        //         return;
        //     }

        //     DateTime end = DateTime.UtcNow;

        //     //повторно вычичляю потому что я панк
        //     int totalLost = (int)validSummaries.Sum(up => up.writer.skipInfos.Sum(skip => (skip.whenEnded - skip.whenStarted).TotalSeconds));
        //     int advertLost = (int)stream.advertismentSeconds;

        //     decimal? streamCost = stream.pricer?.EstimateAll(end);

        //     string[] videosIds = validSummaries.Select(v => v.videoId!).ToArray();
        //     //какой нул
        //     YoutubeDescriptor you = new(Program.config.YouTube!, videosIds);

        //     //на всякий подождём
        //     await Task.Delay(TimeSpan.FromSeconds(10));

        //     IList<Google.Apis.YouTube.v3.Data.Video> videos;
        //     try
        //     {
        //         Log("Качаем первый лист видосов...");
        //         videos = await you.CheckProcessing();

        //         foreach (var video in videos)
        //             LogList(video);
        //     }
        //     catch (Exception e)
        //     {
        //         Log($"Не удалось скачать лист видосов.\n{e}");
        //         return;
        //     }

        //     //супер дурка, я хз
        //     foreach (var up in validSummaries.ToArray())
        //     {
        //         if (!you.Check(up.videoId!))
        //         {
        //             Log($"А видео то и нет {up.videoId}");
        //             validSummaries.Remove(up);
        //         }
        //     }

        //     //Обновляем первый раз
        //     foreach (var up in validSummaries)
        //     {
        //         string description = FormDescription(up.writer, subgifters,
        //                                                 totalLost, advertLost,
        //                                                 up.conversionTime, totalConversionTime,
        //                                                 up.uploadTime, totalUploadTime,
        //                                                 null,
        //                                                 streamCost);

        //         try
        //         {
        //             Log($"Обновляем видево {up.videoId}...");
        //             await you.UpdateDescription(up.videoId!, description);
        //             Log($"Обновили видево {up.videoId}.");
        //         }
        //         catch (Exception e)
        //         {
        //             Log($"Не удалось обновить {up.videoId}.\n{e}");
        //             continue;
        //         }
        //     }

        //     //ну всё, начинается цирк
        //     //TODO доделать
        //     //await Task.Delay(Program.config.YouTube!.VideoDescriptionUpdateDelay);
        // }

        private string FormName(DateTimeOffset date, int? videoNumber)
        {
            const int limit = 100;

            StringBuilder builder = new();

            builder.Append(date.ToString("dd.MM.yyyy"));

            if (videoNumber != null)
            {
                builder.Append(" // ");
                builder.Append(videoNumber.Value);
            }

            string[] games = stream.timestamper.timestamps.Where(timeS => timeS is GameTimestamp)
                                                          .Select(timeS => ((GameTimestamp)timeS).gameName ?? "???")
                                                          .Distinct()
                                                          .ToArray();

            if (games.Length > 0)
            {
                builder.Append(" // ");

                for (int i = 0; i < games.Length; i++)
                {
                    string game = games[i];
                    string? nextGame = (i + 1) < games.Length ? games[i + 1] : null;

                    int length = game.Length;
                    if (nextGame != null)
                    {
                        length += ", ".Length + nextGame.Length;
                    }

                    if (builder.Length + length <= limit)
                    {
                        builder.Append(game);

                        if (nextGame != null)
                            builder.Append(", ");
                    }
                    else if (builder.Length + "...".Length <= limit)
                    {
                        builder.Append("...");
                        break;
                    }
                }
            }

            return builder.ToString();
        }

        private string FormDescription(VideoWriter video, string[] subgifters,
            int totalLostTimeSeconds, int advertismentSeconds,
            TimeSpan? processingTime, TimeSpan? totalProcessingTime,
            TimeSpan? uploadTime, TimeSpan? totalUploadTime,
            TimeSpan? youtubeProcessingTime,
            decimal? streamCost)
        {
            var videoStartTime = video.linkedThing.firstSegmentDate!.Value;
            var skips = video.skipInfos;

            StringBuilder builder = new();
            builder.AppendLine("Здесь ничего нет, в будущем я стану человеком");

            builder.AppendLine();
            foreach (var subgifter in subgifters)
            {
                builder.AppendLine($"Спасибо за подписку: {subgifter}");
            }

            if (stream.timestamper.timestamps.Count == 0)
            {
                builder.AppendLine();
                builder.AppendLine("Инфы нет, потому что я клоун");
            }
            else
            {
                builder.AppendLine();

                bool first = true;
                foreach (var stamp in stream.timestamper.timestamps)
                {
                    if (stamp is OfflineTimestamp)
                        continue;

                    string status;
                    if (first)
                    {
                        first = false;

                        status = MakeTimestampStr(TimeSpan.FromSeconds(0), stamp.ToString());
                    }
                    else
                    {
                        status = GetCheckStatusString(stamp, videoStartTime, skips);
                    }

                    builder.AppendLine(status);
                }
            }

            string? conversionStr = MakeSomeString("Обработка заняла:", processingTime, totalProcessingTime);
            string? uploadStr = MakeSomeString("Загрузка заняла:", uploadTime, totalUploadTime);
            if (conversionStr != null || uploadStr != null || youtubeProcessingTime != null)
            {
                builder.AppendLine();

                if (conversionStr != null)
                    builder.AppendLine(conversionStr);
                if (uploadStr != null)
                    builder.AppendLine(uploadStr);
                if (youtubeProcessingTime != null) //временно так то, если не впадлу будет
                    builder.AppendLine($"Ютуб обрабатывал видео {youtubeProcessingTime.Value.TotalMinutes:n2} минут.");
            }

            builder.AppendLine();

            builder.AppendLine($"Пропущено секунд всего: {totalLostTimeSeconds}");
            builder.AppendLine($"Пропущено секунд из-за рекламы: {advertismentSeconds}");

            if (streamCost != null)
            {
                builder.AppendLine();

                builder.AppendLine($"Примерная стоимость создания записи стрима: ${streamCost}");
            }

            return builder.ToString();
        }

        private string GetCheckStatusString(BaseTimestamp timestamp, DateTimeOffset videoStartTime, List<SkipInfo> skips)
        {
            TimeSpan onVideoTime = GetOnVideoTime(videoStartTime, timestamp.timestamp, skips);

            if (onVideoTime.Ticks < 0)
            {
                Log($"Ticks < 0 {timestamp.timestamp}");
                onVideoTime = TimeSpan.FromSeconds(0);
            }

            return MakeTimestampStr(onVideoTime, timestamp.ToString());
        }

        private static string MakeTimestampStr(TimeSpan onVideoTime, string content)
        {
            string timeStr = new DateTime(onVideoTime.Ticks).ToString("HH:mm:ss");

            return $"{timeStr} {content}";
        }

        private static TimeSpan GetOnVideoTime(DateTimeOffset videoStartTime, DateTimeOffset absoluteDate, List<SkipInfo> skips)
        {
            //Время на видео, это абсолютное время (date) минус все скипы, которые произошли до этого момента минус время начала видео

            DateTimeOffset result = absoluteDate;

            var ourSkips = skips.Where(skip => skip.whenStarted < absoluteDate).ToArray();

            foreach (SkipInfo skip in ourSkips)
            {
                if (skip.whenEnded <= absoluteDate)
                {
                    //скип целиком находится до даты, целиком его вырезаем
                    result -= skip.whenEnded - skip.whenStarted;
                }
                else
                {
                    //дата находится в скипе, вырезаем часть скипа до даты
                    result -= absoluteDate - skip.whenStarted;
                }
            }

            return result - videoStartTime;
        }

        //не могу придумать как назвать
        private static string? MakeSomeString(string prefix, TimeSpan? localTime, TimeSpan? globalTime)
        {
            if (localTime != null && globalTime != null)
            {
                if (localTime.Value.Ticks != globalTime.Value.Ticks)
                    return $"{prefix} {globalTime.Value.TotalMinutes:n2} ({localTime.Value.TotalMinutes:n2}) минут.";
                else
                    return $"{prefix} {globalTime.Value.TotalMinutes:n2} минут.";
            }
            else if (localTime != null)
            {
                return $"{prefix} ... ({localTime.Value.TotalMinutes:n2}) минут.";
            }
            else if (globalTime != null)
            {
                return $"{prefix} {globalTime.Value.TotalMinutes:n2} минут.";
            }

            return null;
        }

        private void LogList(Google.Apis.YouTube.v3.Data.Video video)
        {
            //video.Status;
            //video.ProcessingDetails;
            //video.Suggestions;

            Log("Video");
            if (video.Status != null)
            {
                Log($"Status: {video.Status.UploadStatus}");
                if (video.Status.FailureReason != null)
                    Log($"FailureReason: {video.Status.FailureReason}");
                if (video.Status.RejectionReason != null)
                    Log($"RejectionReason: {video.Status.RejectionReason}");
            }

            if (video.ProcessingDetails != null)
            {
                Log($"Processing: {video.ProcessingDetails.ProcessingStatus}");

                if (video.ProcessingDetails.ProcessingProgress != null)
                {
                    Log($"Parts: {video.ProcessingDetails.ProcessingProgress.PartsProcessed}/{video.ProcessingDetails.ProcessingProgress.PartsTotal}");
                    Log($"MS left: {video.ProcessingDetails.ProcessingProgress.TimeLeftMs}");
                }
                if (video.ProcessingDetails.ProcessingFailureReason != null)
                    Log($"ProcessingFailureReason: {video.ProcessingDetails.ProcessingFailureReason}");

                if (video.ProcessingDetails.ProcessingIssuesAvailability != null)
                    Log($"ProcessingIssuesAvailability: {video.ProcessingDetails.ProcessingIssuesAvailability}");

                if (video.ProcessingDetails.TagSuggestionsAvailability != null)
                    Log($"TagSuggestionsAvailability: {video.ProcessingDetails.TagSuggestionsAvailability}");

                if (video.ProcessingDetails.EditorSuggestionsAvailability != null)
                    Log($"EditorSuggestionsAvailability: {video.ProcessingDetails.EditorSuggestionsAvailability}");
            }

            if (video.Suggestions != null)
            {
                Log("Suggestions");

                if (video.Suggestions.ProcessingErrors != null)
                {
                    Log($"ProcessingErrors:");
                    foreach (var error in video.Suggestions.ProcessingErrors)
                    {
                        Log(error);
                    }
                }
                if (video.Suggestions.ProcessingWarnings != null)
                {
                    Log($"ProcessingWarnings:");
                    foreach (var warning in video.Suggestions.ProcessingWarnings)
                    {
                        Log(warning);
                    }
                }

                if (video.Suggestions.ProcessingHints != null)
                {
                    Log($"ProcessingHints:");
                    foreach (var hint in video.Suggestions.ProcessingHints)
                    {
                        Log(hint);
                    }
                }
            }
        }
    }
}