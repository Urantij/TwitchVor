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

            if (stream.currentVideoWriter == null)
            {
                Log("Охуенный стрим без видео.");
                secondVolumeOperator = null;
                goto end;
            }

            List<VideoWriter> videoWriters = new();
            videoWriters.AddRange(stream.pastVideoWriters);
            videoWriters.Add(stream.currentVideoWriter);

            if (Program.config.YouTube != null)
            {
                int totalLost = (int)videoWriters.Sum(video => video.skipInfos.Sum(skip => (skip.whenEnded - skip.whenStarted).TotalSeconds));
                int advertLost = (int)stream.advertismentSeconds;

                if (Program.config.ConvertToMp4 && Program.config.Ocean != null)
                {
                    var maxSizeBytes = videoWriters.Select(w => new FileInfo(w.linkedThing.FilePath).Length).Max();

                    //Можете кибербулить меня, но я хуй знает, че там у до на уме.
                    //var maxSizeGB = (int)(maxSizeBytes / 1024d / 1024d / 1024d * 1.1d);
                    var maxSizeGB = (int)Math.Ceiling(maxSizeBytes / 1000d / 1000d / 1000d * 1.1d);

                    string volumeName = DigitalOceanVolumeCreator.GenerateVolumeName(DateTime.UtcNow);

                    Log($"Creating second volume, size ({maxSizeGB})...");
                    DigitalOceanVolumeCreator creator = new(Program.config.Ocean, volumeName);

                    secondVolumeOperator = await creator.CreateAsync();

                    Log($"Created second volume.");

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                else
                {
                    secondVolumeOperator = null;
                }

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

                        var passed = DateTime.UtcNow - startTime;

                        if (converted)
                        {
                            Log($"Converted. Took {passed.TotalMinutes} minutes.");

                            video.linkedThing.SetPath(newPath);
                            video.linkedThing.SetName(newName);
                        }
                        else
                        {
                            Log($"Could not convert. Took {passed.TotalMinutes} minutes.");

                            try
                            {
                                File.Delete(newPath);

                                Log("Deleted converted file");
                            }
                            catch (Exception e)
                            {
                                Log($"Could not delete converted file: {e.Message}");
                            }
                        }
                    }

                    string videoName = FormName(stream.handlerCreationDate, videoWriters.Count == 1 ? (int?)null : videoIndex + 1);
                    string description = FormDescription(video.linkedThing.firstSegmentDate.Value, video.skipInfos, totalLost, advertLost);

                    YoutubeUploader uploader = new(Program.config.YouTube);

                    Log($"Uploading {video.linkedThing.FilePath}...");
                    bool uploaded;
                    using (var fileStream = new FileStream(video.linkedThing.FilePath, FileMode.Open))
                    {
                        uploaded = await uploader.UploadAsync(videoName, description, Program.config.YouTube.VideoTags, fileStream, "public");
                    }

                    if (uploaded)
                    {
                        Log($"Finished uploading {video.linkedThing.FilePath}. Removing from disk...");
                        File.Delete(video.linkedThing.FilePath);
                    }
                    else
                    {
                        Log($"Could not upload video! {video.linkedThing.FilePath}");
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
            else secondVolumeOperator = null;

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

        private string FormDescription(DateTimeOffset videoStartTime, List<SkipInfo> skips, int totalLostTimeSeconds, int advertismentSeconds)
        {
            StringBuilder builder = new();
            builder.AppendLine("Здесь ничего нет, в будущем я стану человеком");
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

            builder.AppendLine($"Пропущено секунд всего: {totalLostTimeSeconds}");
            builder.AppendLine($"Пропущено секунд из-за рекламы: {advertismentSeconds}");

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
    }
}