// using System.Collections.Generic;
// using Google.Apis.Auth.OAuth2;
// using Google.Apis.Auth.OAuth2.Flows;
// using Google.Apis.Auth.OAuth2.Responses;
// using Google.Apis.Services;
// using Google.Apis.Util;
// using Google.Apis.YouTube.v3;
// using Google.Apis.YouTube.v3.Data;

// namespace TwitchVor.Upload.TubeYou
// {
//     class YoutubeDescriptor
//     {
//         private readonly YoutubeCreds creds;
//         private readonly string[] videosIds;

//         private Dictionary<string, VideoSnippet>? dictSnippet;

//         public YoutubeDescriptor(YoutubeCreds creds, string[] videosIds)
//         {
//             this.creds = creds;
//             this.videosIds = videosIds;
//         }

//         //не знаю, насколько реально
//         /// <exception cref="Exception">Если нул</exception>
//         public bool Check(string id)
//         {
//             if (dictSnippet == null)
//                 throw new Exception();

//             return dictSnippet.ContainsKey(id);
//         }

//         /// <exception cref="Exception">Может, что то падает. Может нет. Я хз.</exception>
//         public async Task<IList<Video>> CheckProcessing()
//         {
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
//             }), "user", token);

//             var youtubeService = new YouTubeService(new BaseClientService.Initializer()
//             {
//                 HttpClientInitializer = credentials,
//                 ApplicationName = "Who who who"
//             });

//             //https://developers.google.com/youtube/v3/docs/videos/list
//             var listRequest = youtubeService.Videos.List(new string[] { "id", "snippet", "status", "processingDetails", "suggestions" });
//             listRequest.Id = videosIds;

//             var list = await listRequest.ExecuteAsync();
//             if (list == null)
//                 throw new Exception($"{nameof(list)} is null");

//             if (dictSnippet == null)
//             {
//                 dictSnippet = list.Items.ToDictionary(key => key.Id, value => value.Snippet);
//             }

//             //Возможно ли, что если все видосы загрузились, то тут их не будет?
//             //НЕ ЗНАЮ. Хочу верить что такого быть не может.

//             return list.Items;
//         }

//         /// <exception cref="Exception">Может, что то падает. Может нет. Я хз.</exception>
//         public async Task UpdateDescription(string videoId, string newDescription)
//         {
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
//             }), "user", token);

//             var youtubeService = new YouTubeService(new BaseClientService.Initializer()
//             {
//                 HttpClientInitializer = credentials,
//                 ApplicationName = "Who who who"
//             });

//             //пусть крашится
//             var videoSnippet = dictSnippet!.GetValueOrDefault(videoId);
//             videoSnippet!.Description = newDescription;

//             var video = new Video()
//             {
//                 Id = videoId,
//                 Snippet = videoSnippet,
//             };

//             var updateRequest = youtubeService.Videos.Update(video, new string[] { "snippet" });

//             await updateRequest.ExecuteAsync();
//         }
//     }
// }