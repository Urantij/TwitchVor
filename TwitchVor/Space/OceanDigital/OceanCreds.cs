// using Newtonsoft.Json;

// namespace TwitchVor.Space.OceanDigital
// {
//     class OceanCreds
//     {
//         [JsonProperty(Required = Required.Always)]
//         public string ApiToken { get; set; } = "";
//         [JsonProperty(Required = Required.Always)]
//         public long DropletId { get; set; }

//         [JsonProperty(Required = Required.Always)]
//         public int SizeGigabytes { get; set; }

//         [JsonProperty(Required = Required.Always)]
//         /// <summary>
//         /// А Я НЕ СДЕЛАЛ))
//         /// </summary>
//         public bool UseTempVideoWriter { get; set; } = false;

//         //В рантайме достану по дроплету
//         [JsonIgnore]
//         public string Region { get; set; } = "";
//     }
// }