using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;

namespace TwitchVor.Upload.TubeYou;

class YoutubeUploader : BaseUploader
{
    private readonly YoutubeCreds creds;

    // TODO сделать нормально
    private const string mimeType = "video/mp4";

    public override long SizeLimit => 1000L * 1000L * 1000L * 256L;

    public override TimeSpan DurationLimit => TimeSpan.FromHours(12);

    readonly List<YoutubeVideoInfo> uploadedVideos = new();

    Task? postUploadTask = null;

    public YoutubeUploader(Guid guid, ILoggerFactory loggerFactory, YoutubeCreds creds) : base(guid, loggerFactory)
    {
        this.creds = creds;
    }

    public override async Task<bool> UploadAsync(UploaderHandler uploaderHandler, ProcessingVideo processingVideo,
        string name, string description, string fileName, long size, Stream content)
    {
        var secrets = new ClientSecrets()
        {
            ClientId = creds.ClientId,
            ClientSecret = creds.ClientSecret
        };

        var token = new TokenResponse { RefreshToken = creds.RefreshToken };
        var credentials = new UserCredential(new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets
            }), creds.UserId, token);

        var youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credentials,
            ApplicationName = "Who read this will die"
        });

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = name,
                Description = description,
                Tags = creds.VideoTags,
                DefaultLanguage = "ru",
                DefaultAudioLanguage = "ru",
                CategoryId = "22" // See https://developers.google.com/youtube/v3/docs/videoCategories/list
            },
            Status = new VideoStatus()
            {
                PrivacyStatus = "public", // "unlisted" or "private" or "public"
                SelfDeclaredMadeForKids = false,
            }
        };

        var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", content, mimeType);
        videosInsertRequest.ChunkSize = ResumableUpload.DefaultChunkSize;

        string? videoId = null;
        videosInsertRequest.UploadSessionData += (data) => { };
        videosInsertRequest.ResponseReceived += (response) =>
        {
            videoId = response.Id;
            _logger.LogDebug("Айди видео: {id}", videoId);
        };

        var progress = await videosInsertRequest.UploadAsync();

        if (progress.Exception != null)
        {
            if (progress.Exception is AggregateException aggregateException)
            {
                foreach (var ex in aggregateException.InnerExceptions)
                {
                    _logger.LogError(ex, "Одна из ошибок при загрузке видео.");
                }
            }
            else
            {
                _logger.LogError(progress.Exception, "Ошибка при загрузке видео.");
            }
        }

        if (progress.Status == UploadStatus.Completed)
        {
            if (videoId != null)
            {
                YoutubeVideoInfo youtubeVideoInfo = new(processingVideo, videoId);
                uploadedVideos.Add(youtubeVideoInfo);

                if (postUploadTask == null)
                    postUploadTask = PostProcessWorkAsync(uploaderHandler);
            }
            else
            {
                _logger.LogError("Видео загружено успешно, но нет айди.");
            }
        }
        else
        {
            _logger.LogError("Плохой статус при загрузке видео {status}", progress.Status);
        }

        return progress.Status == UploadStatus.Completed;
    }

    async Task PostProcessWorkAsync(UploaderHandler uploaderHandler)
    {
        await uploaderHandler.processingHandler.ProcessTask;

        await Task.Delay(TimeSpan.FromSeconds(10));

        var secrets = new ClientSecrets()
        {
            ClientId = creds.ClientId,
            ClientSecret = creds.ClientSecret
        };

        var token = new TokenResponse { RefreshToken = creds.RefreshToken };
        var credentials = new UserCredential(new GoogleAuthorizationCodeFlow(
            new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets
            }), creds.UserId, token);

        var youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credentials,
            ApplicationName = "Who read this will die"
        });

        var listRequest = youtubeService.Videos.List(new string[]
            { "id", "snippet", "status", "processingDetails", "suggestions" });
        listRequest.Id = uploadedVideos.Select(v => v.videoId).ToArray();

        var listResponse = await listRequest.ExecuteAsync();

        foreach (var video in uploadedVideos)
        {
            var response = listResponse.Items.FirstOrDefault(i => i.Id == video.videoId);
            if (response == null)
            {
                _logger.LogError("Не удалось найти ответ на {id}", video.videoId);
                continue;
            }

            var videoUpdate = new Video()
            {
                Id = video.videoId,
                Snippet = response.Snippet
            };

            // А мне вот не дало загрузить видео, потому что стрелочка в описании была.
            // В названии тоже нельзя.
            // Было бы здорово, если бы эта информация была более общедоступна, но увы, ютуб контора          .
            videoUpdate.Snippet.Title =
                uploaderHandler.MakeVideoName(video.processingVideo).Replace(">", "").Replace("<", "");
            videoUpdate.Snippet.Description = uploaderHandler.MakeVideoDescription(video.processingVideo)
                .Replace(">", "").Replace("<", "");

            try
            {
                var updateRequest = youtubeService.Videos.Update(videoUpdate, new string[] { "snippet" });
                await updateRequest.ExecuteAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Не удалось изменить описание видео {id}", video.videoId);
                continue;
            }

            _logger.LogInformation("Изменили описание видно {id}", video.videoId);
        }
    }
}