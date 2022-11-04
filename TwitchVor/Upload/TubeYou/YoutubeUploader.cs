// using Google.Apis.Auth.OAuth2;
// using Google.Apis.Auth.OAuth2.Flows;
// using Google.Apis.Auth.OAuth2.Responses;
// using Google.Apis.Services;
// using Google.Apis.Upload;
// using Google.Apis.YouTube.v3;
// using Google.Apis.YouTube.v3.Data;

// namespace TwitchVor.Upload.TubeYou
// {
//     public class YoutubeUploader
//     {
//         private CancellationTokenSource? cancellationTokenSource;

//         private readonly YoutubeCreds creds;

//         //TODO а тута не должен быть другой тип, если конверт или нет? :)
//         private const string mimeType = "video/MP2T";

//         public string? videoId;

//         public YoutubeUploader(YoutubeCreds creds)
//         {
//             this.creds = creds;
//         }

//         static void Log(string message)
//         {
//             Utility.ColorLog.Log(message, "YouUp", ConsoleColor.Red, ConsoleColor.White);
//         }

//         static void LogError(string message)
//         {
//             Utility.ColorLog.LogError(message, "YouUp");
//         }

//         public void Stop()
//         {
//             cancellationTokenSource?.Cancel();
//         }

//         /// <param name="privateString">"unlisted" or "private" or "public"</param>
//         public async Task<bool> UploadAsync(string name, string description, string[] tags, FileStream fs, string privateString)
//         {
//             //https://developers.google.com/youtube/v3/guides/using_resumable_upload_protocol

//             cancellationTokenSource = new CancellationTokenSource();

//             var secrets = new ClientSecrets()
//             {
//                 ClientId = creds.ClientId,
//                 ClientSecret = creds.ClientSecret
//             };

//             var token = new TokenResponse { RefreshToken = creds.RefreshToken };
//             var credentials = new UserCredential(new GoogleAuthorizationCodeFlow(
//             new GoogleAuthorizationCodeFlow.Initializer
//             {
//                 ClientSecrets = secrets
//             }), creds.UserId, token);

//             var youtubeService = new YouTubeService(new BaseClientService.Initializer()
//             {
//                 HttpClientInitializer = credentials,
//                 ApplicationName = "Who read this will die"
//             });

//             var video = new Video
//             {
//                 Snippet = new VideoSnippet
//                 {
//                     Title = name,
//                     Description = description,
//                     Tags = tags,
//                     DefaultLanguage = "ru",
//                     CategoryId = "22" // See https://developers.google.com/youtube/v3/docs/videoCategories/list
//                 },
//                 Status = new VideoStatus()
//                 {
//                     PrivacyStatus = privateString, // "unlisted" or "private" or "public"
//                     SelfDeclaredMadeForKids = false,
//                 }
//             };

//             var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fs, mimeType);
//             videosInsertRequest.ChunkSize = ResumableUpload.DefaultChunkSize;

//             videosInsertRequest.UploadSessionData += VideosInsertRequest_UploadSessionData;
//             videosInsertRequest.ResponseReceived += VideosInsertRequest_ResponseReceived;

//             var progress = await videosInsertRequest.UploadAsync(cancellationTokenSource.Token);

//             if (progress.Exception != null)
//             {
//                 if (progress.Exception is AggregateException aggregateException)
//                 {
//                     LogError($"Aggr Exception\n{string.Join('\n', aggregateException.InnerExceptions)}");
//                 }
//                 else
//                 {
//                     LogError($"Exception\n{progress.Exception}");
//                 }
//             }

//             if (progress.Status != UploadStatus.Completed)
//             {
//                 LogError($"Status: {progress.Status}");
//             }

//             return progress.Status == UploadStatus.Completed;
//         }

//         private void VideosInsertRequest_ResponseReceived(Video obj)
//         {
//             videoId = obj.Id;
//             Log($"Video id is {videoId}");
//         }

//         private void VideosInsertRequest_UploadSessionData(IUploadSessionData obj)
//         {

//         }

//         private void LogList(Google.Apis.YouTube.v3.Data.Video video)
//         {
//             //video.Status;
//             //video.ProcessingDetails;
//             //video.Suggestions;

//             Log("Video");
//             if (video.Status != null)
//             {
//                 Log($"Status: {video.Status.UploadStatus}");
//                 if (video.Status.FailureReason != null)
//                     Log($"FailureReason: {video.Status.FailureReason}");
//                 if (video.Status.RejectionReason != null)
//                     Log($"RejectionReason: {video.Status.RejectionReason}");
//             }

//             if (video.ProcessingDetails != null)
//             {
//                 Log($"Processing: {video.ProcessingDetails.ProcessingStatus}");

//                 if (video.ProcessingDetails.ProcessingProgress != null)
//                 {
//                     Log($"Parts: {video.ProcessingDetails.ProcessingProgress.PartsProcessed}/{video.ProcessingDetails.ProcessingProgress.PartsTotal}");
//                     Log($"MS left: {video.ProcessingDetails.ProcessingProgress.TimeLeftMs}");
//                 }
//                 if (video.ProcessingDetails.ProcessingFailureReason != null)
//                     Log($"ProcessingFailureReason: {video.ProcessingDetails.ProcessingFailureReason}");

//                 if (video.ProcessingDetails.ProcessingIssuesAvailability != null)
//                     Log($"ProcessingIssuesAvailability: {video.ProcessingDetails.ProcessingIssuesAvailability}");

//                 if (video.ProcessingDetails.TagSuggestionsAvailability != null)
//                     Log($"TagSuggestionsAvailability: {video.ProcessingDetails.TagSuggestionsAvailability}");

//                 if (video.ProcessingDetails.EditorSuggestionsAvailability != null)
//                     Log($"EditorSuggestionsAvailability: {video.ProcessingDetails.EditorSuggestionsAvailability}");
//             }

//             if (video.Suggestions != null)
//             {
//                 Log("Suggestions");

//                 if (video.Suggestions.ProcessingErrors != null)
//                 {
//                     Log($"ProcessingErrors:");
//                     foreach (var error in video.Suggestions.ProcessingErrors)
//                     {
//                         Log(error);
//                     }
//                 }
//                 if (video.Suggestions.ProcessingWarnings != null)
//                 {
//                     Log($"ProcessingWarnings:");
//                     foreach (var warning in video.Suggestions.ProcessingWarnings)
//                     {
//                         Log(warning);
//                     }
//                 }

//                 if (video.Suggestions.ProcessingHints != null)
//                 {
//                     Log($"ProcessingHints:");
//                     foreach (var hint in video.Suggestions.ProcessingHints)
//                     {
//                         Log(hint);
//                     }
//                 }
//             }
//         }
//     }
// }