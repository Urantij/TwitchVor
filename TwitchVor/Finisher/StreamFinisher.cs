using System;
using System.Diagnostics;
using System.Text;
using TwitchVor.Ocean;
using TwitchVor.TubeYou;
using TwitchVor.Twitch.Downloader;
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

        /// <summary>
        /// Грузим на ютуб... Удаляем...
        /// </summary>
        /// <param name="stream"></param>
        public async Task Finish()
        {
            bool deleteVolume = true;

            DigitalOceanVolumeOperator? secondVolumeOperator;

            List<UploadedVideo>? uploadedVideos;

            if (stream.currentVideoWriter == null)
            {
                Log("Охуенный стрим без видео.");
                secondVolumeOperator = null; uploadedVideos = null;
                goto end;
            }

            List<VideoWriter> videoWriters = new();
            videoWriters.AddRange(stream.pastVideoWriters);
            videoWriters.Add(stream.currentVideoWriter);

            if (Program.config.YouTube != null)
            {
                uploadedVideos = new();

                if (Program.config.ConvertToMp4 && Program.config.Ocean != null)
                {
                    var maxSizeBytes = videoWriters.Select(w => new FileInfo(w.linkedThing.FilePath).Length).Max();

                    //Можете кибербулить меня, но я хуй знает, че там у до на уме.
                    //var maxSizeGB = (int)(maxSizeBytes / 1024d / 1024d / 1024d * 1.1d);
                    int maxSizeGB = (int)Math.Ceiling(maxSizeBytes / 1000M / 1000M / 1000M * 1.1M);

                    Log($"Creating second volume, size ({maxSizeGB})...");

                    if (maxSizeBytes < 10)
                    {
                        maxSizeBytes = 10;
                        Log("Volume is too small, set size to 10GB");
                    }

                    string volumeName = DigitalOceanVolumeCreator.GenerateVolumeName(DateTime.UtcNow);

                    DigitalOceanVolumeCreator creator = new(Program.config.Ocean, volumeName, maxSizeGB);

                    secondVolumeOperator = await creator.CreateAsync();

                    stream.pricer.AddVolume(DateTime.UtcNow, maxSizeGB);
                    Log($"Created second volume.");

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                else
                {
                    secondVolumeOperator = null;
                }

                int totalLost = (int)videoWriters.Sum(video => video.skipInfos.Sum(skip => (skip.whenEnded - skip.whenStarted).TotalSeconds));
                int advertLost = (int)stream.advertismentSeconds;

                bool allUploaded = true;
                for (int videoIndex = 0; videoIndex < videoWriters.Count; videoIndex++)
                {
                    var video = videoWriters[videoIndex];

                    if (video.linkedThing.estimatedSize == 0 || video.linkedThing.estimatedDuration == 0 || video.linkedThing.firstSegmentDate == null)
                    {
                        Log($"Охуенное видео без сегментов. {videoIndex}");
                        continue;
                    }

                    string originalPath = video.linkedThing.FilePath;
                    TimeSpan? conversionTime;

                    if (Program.config.ConvertToMp4)
                    {
                        Log($"Converting {video.linkedThing.FileName}...");

                        string newName = Path.ChangeExtension(video.linkedThing.FileName, ".mp4");
                        string newPath;

                        if (secondVolumeOperator != null)
                        {
                            newPath = Path.Combine($"/mnt/{secondVolumeOperator.volumeName}", newName);
                        }
                        else
                        {
                            newPath = Path.ChangeExtension(video.linkedThing.FilePath, ".mp4");
                        }

                        DateTime startTime = DateTime.UtcNow;

                        bool converted = Ffmpeg.Convert(originalPath, newPath);

                        conversionTime = DateTime.UtcNow - startTime;

                        if (converted)
                        {
                            Log($"Converted. Took {conversionTime.Value.TotalMinutes} minutes.");

                            video.linkedThing.SetPath(newPath);
                            video.linkedThing.SetName(newName);
                        }
                        else
                        {
                            Log($"Could not convert. Took {conversionTime.Value.TotalMinutes} minutes.");

                            try
                            {
                                File.Delete(newPath);

                                Log("Deleted converted file");
                            }
                            catch (Exception e)
                            {
                                Log($"Could not delete converted file: {e.Message}");
                            }

                            conversionTime = null;
                        }
                    }
                    else
                    {
                        conversionTime = null;
                    }

                    string videoName = FormName(stream.handlerCreationDate, videoWriters.Count == 1 ? (int?)null : videoIndex + 1);
                    string description = FormDescription(video, totalLost, advertLost, null, null, null, null, null, null);

                    YoutubeUploader uploader = new(Program.config.YouTube);

                    TimeSpan uploadTime;

                    Log($"Uploading {video.linkedThing.FilePath}...");
                    bool uploaded;
                    using (var fileStream = new FileStream(video.linkedThing.FilePath, FileMode.Open))
                    {
                        var uploadStartDate = DateTime.UtcNow;
                        uploaded = await uploader.UploadAsync(videoName, description, Program.config.YouTube.VideoTags, fileStream, "public");

                        uploadTime = DateTime.UtcNow - uploadStartDate;
                    }

                    if (uploaded)
                    {
                        Log($"Finished uploading {video.linkedThing.FilePath}. Took {uploadTime.TotalMinutes} minutes.");
                        File.Delete(video.linkedThing.FilePath);
                        Log("Deleted file from disk.");

                        if (uploader.videoId != null)
                        {
                            uploadedVideos.Add(new UploadedVideo(video, uploader.videoId, conversionTime, uploadTime));
                        }
                        else
                        {
                            Log($"{nameof(uploader.videoId)} id is null");
                        }
                    }
                    else
                    {
                        Log($"Could not upload video! {video.linkedThing.FilePath}. Took {uploadTime.TotalMinutes} minutes.");
                        allUploaded = false;

                        File.WriteAllText(Path.ChangeExtension(originalPath, "description.txt"), description);
                    }

                    if (Program.config.ConvertToMp4)
                    {
                        //Если успешно загрузилось, удаляем и новый и старый файл. Если не успешно, только новый
                        //А если успешно, оно новый само удаляет.
                        if (uploaded)
                        {
                            File.Delete(originalPath);
                            Log($"Uploaded, removed original file {originalPath}");
                        }
                        else
                        {
                            File.Delete(video.linkedThing.FilePath);
                            Log($"Could not upload, removed converted file {video.linkedThing.FilePath}");
                        }
                    }
                }

                Log($"Finished. Uploaded all: {allUploaded}");
                deleteVolume = allUploaded;
            }
            else if (Program.config.ConvertToMp4)
            {
                //это не клауд
                secondVolumeOperator = null;
                uploadedVideos = null;

                foreach (var video in videoWriters)
                {
                    Log($"Converting {video.linkedThing.FileName}...");

                    string newName = Path.ChangeExtension(video.linkedThing.FileName, ".mp4");

                    string oldPath = video.linkedThing.FilePath;
                    string newPath = Path.ChangeExtension(video.linkedThing.FilePath, ".mp4");

                    DateTime startTime = DateTime.UtcNow;

                    bool converted = Ffmpeg.Convert(oldPath, newPath);

                    var passed = DateTime.UtcNow - startTime;

                    if (converted)
                    {
                        Log($"Converted. Took {passed.TotalMinutes} minutes.");

                        File.Delete(oldPath);
                        Log("Removed old file.");

                        video.linkedThing.SetPath(newPath);
                        video.linkedThing.SetName(newName);
                    }
                    else
                    {
                        Log($"Could not convert. Took {passed.TotalMinutes} minutes.");
                    }
                }
            }
            else
            {
                secondVolumeOperator = null; uploadedVideos = null;
            }

        end:;

            List<DigitalOceanVolumeOperator> operators = new();

            if (secondVolumeOperator != null)
                operators.Add(secondVolumeOperator);

            //не может быть нул если второй не нул, ну да ладно
            if (stream.volumeOperator2 != null)
                operators.Add(stream.volumeOperator2);

            if (operators.Count == 0)
                return;

            //расправа

            foreach (var op in operators)
            {
                Log($"Detaching volume {op.volumeName}...");

                await op.DetachAsync();

                //Сразу он выдаёт ошибку, что нельзя атачд вольюм удалить
                //алсо не знаю, стоит ли их в один момент детачить, так что лучше подожду
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            foreach (var op in operators)
            {
                Log($"Deleting volume {op.volumeName}...");
                try
                {
                    await op.DeleteAsync();
                    Log($"Deleted volume {op.volumeName}.");
                }
                catch (Exception e)
                {
                    Log($"Could not delete volume {op.volumeName}:\n{e}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10));

            //хз че будет, если не удалённому вольюму удалить папку, но мне похуй
            foreach (var op in operators)
            {
                try
                {
                    Directory.Delete($"/mnt/{op.volumeName}");
                    Log($"Directory {op.volumeName} deleted");
                }
                catch (Exception e)
                {
                    Log($"Could not delete directory {op.volumeName}: {e.Message}");
                }
            }

            if (uploadedVideos?.Count > 0)
            {
                ContinueVideoninining(uploadedVideos);
            }
        }

        private async void ContinueVideoninining(List<UploadedVideo> upVideos)
        {
            DateTime end = DateTime.UtcNow;

            //повторно вычичляю потому что я панк
            int totalLost = (int)upVideos.Sum(up => up.writer.skipInfos.Sum(skip => (skip.whenEnded - skip.whenStarted).TotalSeconds));
            int advertLost = (int)stream.advertismentSeconds;

            decimal streamCost = stream.pricer.EstimateAll(end);

            string[] videosIds = upVideos.Select(v => v.videoId).ToArray();
            //какой нул
            YoutubeDescriptor you = new(Program.config.YouTube!, videosIds);

            TimeSpan? totalProcessingTime;
            {
                var allConversions = upVideos.Where(v => v.processingTime != null)
                                             .Select(v => v.processingTime!.Value)
                                             .ToArray();

                if (allConversions.Length > 0)
                {
                    totalProcessingTime = TimeSpan.FromSeconds(0);

                    foreach (var time in allConversions)
                        totalProcessingTime = totalProcessingTime.Value + time;
                }
                else
                {
                    totalProcessingTime = null;
                }
            }

            TimeSpan totalUploadingTime = TimeSpan.FromSeconds(0);
            foreach (var up in upVideos)
                totalUploadingTime += up.uploadingTime;

            //на всякий подождём
            await Task.Delay(TimeSpan.FromSeconds(10));
            IList<Google.Apis.YouTube.v3.Data.Video> videos;
            try
            {
                Log("Making first list...");
                videos = await you.CheckProcessing();

                foreach (var video in videos)
                    LogList(video);
            }
            catch (Exception e)
            {
                Log($"Could not list videos.\n{e}");
                return;
            }

            //супер дурка, я хз
            foreach (var up in upVideos.ToArray())
            {
                if (!you.Check(up.videoId))
                {
                    Log($"no video {up.videoId}");
                    upVideos.Remove(up);
                }
            }

            //Обновляем первый раз
            foreach (var up in upVideos)
            {
                string description = FormDescription(up.writer, totalLost, advertLost,
                                                        up.processingTime, totalProcessingTime,
                                                        up.uploadingTime, totalUploadingTime,
                                                        null,
                                                        streamCost);

                try
                {
                    Log($"Updating video {up.videoId}...");
                    await you.UpdateDescription(up.videoId, description);
                }
                catch (Exception e)
                {
                    Log($"Could not update video {up.videoId}.\n{e}");
                    continue;
                }
            }

            //ну всё, начинается цирк
            //TODO доделать
            //await Task.Delay(Program.config.YouTube!.VideoDescriptionUpdateDelay);
        }

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
                        length += " ".Length + nextGame.Length;
                    }

                    if (builder.Length + length <= limit)
                    {
                        builder.Append(game);

                        if (nextGame != null)
                            builder.Append(' ');
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

        private string FormDescription(VideoWriter video, int totalLostTimeSeconds, int advertismentSeconds,
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

            if (stream.subGifter != null)
            {
                builder.AppendLine();
                builder.AppendLine($"Спасибо за подписку: {stream.subGifter}");
            }

            if (stream.timestamper.timestamps.Count == 0)
            {
                builder.AppendLine("Инфы нет, потому что я клоун");
            }
            else
            {
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

                builder.AppendLine($"Примерная стоимость стрима: ${streamCost}");
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

            return $"{timeStr} - {content}";
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
                return $"{prefix} {globalTime.Value.TotalMinutes:n2} ({localTime.Value.TotalMinutes:n2}) минут.";
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
            Log($"Status: {video.Status.UploadStatus}");
            if (video.Status.FailureReason != null)
                Log($"FailureReason: {video.Status.FailureReason}");
            if (video.Status.RejectionReason != null)
                Log($"RejectionReason: {video.Status.RejectionReason}");

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

            Log("Suggestions");
            if (video.Suggestions.ProcessingErrors != null)
                Log($"ProcessingErrors: {video.Suggestions.ProcessingErrors}");
            if (video.Suggestions.ProcessingWarnings != null)
                Log($"ProcessingWarnings: {video.Suggestions.ProcessingWarnings}");
            if (video.Suggestions.ProcessingHints != null)
                Log($"ProcessingHints: {video.Suggestions.ProcessingHints}");
        }
    }
}