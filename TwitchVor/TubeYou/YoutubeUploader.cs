using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace TwitchVor.TubeYou
{
    public class YoutubeUploader
    {
        private CancellationTokenSource? cancellationTokenSource;

        private readonly YoutubeCreds creds;

        //TODO а тута не должен быть другой тип, если конверт или нет? :)
        private const string mimeType = "video/MP2T";

        public string? videoId;

        public YoutubeUploader(YoutubeCreds creds)
        {
            this.creds = creds;
        }

        static void Log(string message)
        {
            Utility.ColorLog.Log(message, "YouUp", ConsoleColor.Red, ConsoleColor.White);
        }

        static void LogError(string message)
        {
            Utility.ColorLog.LogError(message, "YouUp");
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
        }

        /// <param name="privateString">"unlisted" or "private" or "public"</param>
        public async Task<bool> UploadAsync(string name, string description, string[] tags, FileStream fs, string privateString)
        {
            //https://developers.google.com/youtube/v3/guides/using_resumable_upload_protocol

            cancellationTokenSource = new CancellationTokenSource();

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
            }), "user", token);

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
                    Tags = tags,
                    DefaultLanguage = "ru",
                    CategoryId = "22" // See https://developers.google.com/youtube/v3/docs/videoCategories/list
                },
                Status = new VideoStatus()
                {
                    PrivacyStatus = privateString, // "unlisted" or "private" or "public"
                    SelfDeclaredMadeForKids = false,
                }
            };

            var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fs, mimeType);
            videosInsertRequest.ChunkSize = ResumableUpload.DefaultChunkSize;

            videosInsertRequest.UploadSessionData += VideosInsertRequest_UploadSessionData;
            videosInsertRequest.ResponseReceived += VideosInsertRequest_ResponseReceived;

            var progress = await videosInsertRequest.UploadAsync(cancellationTokenSource.Token);

            if (progress.Exception != null)
            {
                if (progress.Exception is AggregateException aggregateException)
                {
                    LogError($"Aggr Exception\n{string.Join('\n', aggregateException.InnerExceptions)}");
                }
                else
                {
                    LogError($"Exception\n{progress.Exception}");
                }
            }

            if (progress.Status != UploadStatus.Completed)
            {
                LogError($"Status: {progress.Status}");
            }

            return progress.Status == UploadStatus.Completed;
        }

        private void VideosInsertRequest_ResponseReceived(Video obj)
        {
            videoId = obj.Id;
            Log($"Video id is {videoId}");
        }

        private void VideosInsertRequest_UploadSessionData(IUploadSessionData obj)
        {

        }
    }
}