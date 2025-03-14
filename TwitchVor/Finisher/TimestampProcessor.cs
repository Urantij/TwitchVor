using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchLib.Api.Helix.Models.Videos.GetVideos;
using TwitchVor.Main;
using TwitchVor.Twitch.Chat;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Vvideo;

namespace TwitchVor.Finisher;

internal static class TimestampProcessor
{
    public static async Task DoClips(StreamHandler streamHandler, List<BaseTimestamp> timestamps, ILogger logger)
    {
        Clip[] clips = streamHandler.chatWorker.CloneFetchedClips();
        if (clips.Length == 0)
            return;

        try
        {
            clips = clips.Where(clip =>
            {
                DateTime date = DateTime.Parse(clip.CreatedAt);

                return date > streamHandler.handlerCreationDate;
            }).ToArray();

            if (clips.Length == 0)
                return;

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
                        logger.LogWarning("Клип {id} не найден в обновлённом списке.", clip.Id);
                    }

                    return updatedClip ?? clip;
                }).ToArray();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Не удалось обновить информацию о клипах.");
            }

            List<string> videoIds = clips.Select(clip => clip.VideoId)
                .Distinct()
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            Video[]? videos = null;
            if (videoIds.Count > 0)
            {
                try
                {
                    GetVideosResponse vids =
                        await Program.twitchAPI.Helix.Videos.GetVideosAsync(videoIds: videoIds);

                    videos = vids.Videos;
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Не удалось получить ответ с видиками.");
                }
            }
            else
            {
                logger.LogWarning("Нет видео для клипов.");
            }

            // Бредик
            DateTime EstimateClipLocation(Clip clip, DateTime created, ChatClipConfig config)
            {
                return created - TimeSpan.FromSeconds(clip.Duration) + config.ClipOffset;
            }

            List<ChatClipTimestamp> clipStamps = new();
            foreach (Clip clip in clips)
            {
                // этот код выглядит всрато, но я не буду его переписывать.
                Video? video = videos?.FirstOrDefault(v => v.Id == clip.VideoId);

                DateTime clipDate;
                if (video != null)
                {
                    // Иногда в чат кидают новые клипы со старых водов. Удивительное дело.
                    DateTime videoCreatedAt = DateTime.Parse(video.CreatedAt);

                    // Небольшой оффсет, так как непонятно...
                    if (videoCreatedAt + TimeSpan.FromMinutes(30) < streamHandler.handlerCreationDate)
                    {
                        logger.LogInformation("Кто то закинул старый клип... хух {clip}", clip.Id);
                        continue;
                    }

                    if (clip.VodOffset != 0)
                    {
                        clipDate = videoCreatedAt + TimeSpan.FromSeconds(clip.VodOffset);

                        clipStamps.Add(new ChatClipTimestamp(clip.CreatorName, clip.CreatorId,
                            clip.Title, clip.Url, clipDate));
                        continue;
                    }

                    logger.LogWarning("Для клипа нет оффсета {clip}", clip.Id);
                }
                else
                {
                    logger.LogWarning("Не удалось найти видео для клипа {clip} ({id})", clip.Id,
                        clip.VideoId);
                }

                DateTime clipCreatedAt = DateTime.Parse(clip.CreatedAt);

                clipDate = EstimateClipLocation(clip, clipCreatedAt, Program.config.Chat?.FetchClips);

                clipStamps.Add(new ChatClipTimestamp(clip.CreatorName, clip.CreatorId,
                    clip.Title, clip.Url, clipDate));
            }

            timestamps.AddRange(clipStamps);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось проанализировать твич воды.");
        }
    }
}