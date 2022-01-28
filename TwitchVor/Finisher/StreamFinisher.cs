using System.Text;
using TwitchVor.TubeYou;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Utility;
using TwitchVor.Vvideo;

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

            if (Program.config.YouTube != null)
            {
                if (stream.currentVideoWriter == null)
                {
                    Log("Охуенный стрим без видео.");
                    goto end;
                }

                List<VideoWriter> videoWriters = new();
                videoWriters.AddRange(stream.pastVideoWriters);
                videoWriters.Add(stream.currentVideoWriter);

                bool allUploaded = true;
                for (int videoIndex = 0; videoIndex < videoWriters.Count; videoIndex++)
                {
                    var video = videoWriters[videoIndex];

                    if (video.linkedThing.estimatedSize == 0 || video.linkedThing.estimatedDuration == 0 || video.linkedThing.firstSegmentDate == null)
                    {
                        Log($"Охуенное видео без сегментов. {videoIndex}");
                        continue;
                    }

                    string videoName = FormName(stream.handlerCreationDate, videoWriters.Count == 1 ? (int?)null : videoIndex + 1);
                    string description = FormDescription(video.linkedThing.firstSegmentDate.Value, video.skipInfos, stream.advertismentSeconds);

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

                        File.WriteAllText(Path.ChangeExtension(video.linkedThing.FilePath, "description.txt"), description);
                    }
                }

                Log($"Finished. Uploaded all: {allUploaded}");
                deleteVolume = allUploaded;
            }

            end:;
            if (stream.volumeOperator2 != null && deleteVolume)
            {
                Log("Detaching volume...");
                await stream.volumeOperator2.DetachAsync();

                //Сразу он выдаёт ошибку, что нельзя атачд вольюм удалить
                await Task.Delay(TimeSpan.FromSeconds(10));

                Log("Removing volume...");
                try
                {
                    await stream.volumeOperator2.DeleteAsync();
                    Log("Removed volume.");
                }
                catch (Exception e)
                {
                    Log($"Could not remove volume:\n{e}");
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(10));
                try
                {
                    Directory.Delete($"/mnt/{stream.volumeOperator2.volumeName}");
                    Log("Directory deleted");
                }
                catch (Exception e)
                {
                    Log($"Could not delete directory: {e.Message}");
                }
            }
        }

        private string FormName(DateTimeOffset date, int? videoNumber)
        {
            const int limit = 70;

            StringBuilder builder = new();

            builder.Append(date.ToString("dd.MM.yyyy"));

            if (videoNumber != null)
            {
                builder.Append(" // ");
                builder.Append(videoNumber.Value);
            }

            if (stream.timestamper.games.Count > 0)
            {
                builder.Append(" // ");

                string gamesStr = string.Join(", ", stream.timestamper.games);
                if (builder.Length + gamesStr.Length <= limit)
                {
                    builder.Append(gamesStr);
                }
                else
                {
                    builder.Append("...");
                }
            }

            return builder.ToString();
        }

        private string FormDescription(DateTimeOffset videoStartTime, List<SkipInfo> skips, float advertismentSeconds)
        {
            StringBuilder builder = new();
            builder.AppendLine("Здесь ничего нет, в будущем я стану человеком");
            if (stream.timestamper.timestamps.Count == 0)
            {
                builder.AppendLine("Инфы нет, потому что я клоун");
            }
            else
            {
                foreach (var stamps in stream.timestamper.timestamps)
                {
                    string status = GetCheckStatusString(stamps, videoStartTime, skips);

                    builder.AppendLine(status);
                }
            }

            builder.AppendLine($"Пропущено секунд из-за рекламы: {(int)advertismentSeconds}");

            return builder.ToString();
        }

        private string GetCheckStatusString(Timestamp timestamp, DateTimeOffset videoStartTime, List<SkipInfo> skips)
        {
            TimeSpan onVideoTime = GetOnVideoTime(videoStartTime, timestamp.timestamp, skips);

            if (onVideoTime.Ticks < 0)
            {
                Log($"Ticks < 0 {timestamp.timestamp}");
                onVideoTime = TimeSpan.FromSeconds(0);
            }

            string timeStr = new DateTime(onVideoTime.Ticks).ToString("HH:mm:ss");

            return $"{timeStr} - {timestamp.content}";
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