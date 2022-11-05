// using Newtonsoft.Json;

// namespace TwitchVor.Upload.TubeYou
// {
//     public class YoutubeCreds
//     {
//         [JsonProperty(Required = Required.Always)]
//         public string RefreshToken { get; private set; } = "";
//         [JsonProperty(Required = Required.Always)]
//         public string UserId { get; private set; } = "";

//         [JsonProperty(Required = Required.Always)]
//         public string ClientId { get; private set; } = "";
//         [JsonProperty(Required = Required.Always)]
//         public string ClientSecret { get; private set; } = "";

//         [JsonProperty(Required = Required.Always)]
//         public string[] VideoTags { get; private set; } = Array.Empty<string>();

//         /// <summary>
//         /// Сколько ждём, прежде чем начнём дудосить ютуб
//         /// </summary>
//         [JsonProperty(Required = Required.Default)]
//         public TimeSpan VideoDescriptionUpdateDelay = TimeSpan.FromMinutes(30);

//         /// <summary>
//         /// Как часто проверять
//         /// </summary>
//         [JsonProperty(Required = Required.Default)]
//         public TimeSpan VideoProcessingCheckDelay = TimeSpan.FromMinutes(5);

//         public YoutubeCreds()
//         {
//         }

//         public YoutubeCreds(string refreshToken, string userId, string clientId, string clientSecret)
//         {
//             RefreshToken = refreshToken;
//             UserId = userId;
//             ClientId = clientId;
//             ClientSecret = clientSecret;
//         }
//     }
// }