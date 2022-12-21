using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Space.TimeWeb
{
    public class TimewebConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string RefreshToken { get; set; }

        [JsonProperty(Required = Required.Default)]
        public string? AccessToken { get; set; }

        [JsonProperty(Required = Required.Default)]
        public DateTimeOffset? AccessTokenExpirationDate { get; set; }

        [JsonProperty(Required = Required.Default)]
        public bool ValidateTokenOnStart { get; set; } = true;

        [JsonProperty(Required = Required.Default)]
        public TimeSpan RequestsTimeout { get; set; } = TimeSpan.FromSeconds(10);

        [JsonProperty(Required = Required.Default)]
        public long PerFileSize { get; set; } = 100 * 1024 * 1024;

        public TimewebConfig(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpirationDate, bool validateTokenOnStart, TimeSpan requestsTimeout, long perFileSize)
        {
            RefreshToken = refreshToken;
            AccessToken = accessToken;
            AccessTokenExpirationDate = accessTokenExpirationDate;
            ValidateTokenOnStart = validateTokenOnStart;
            RequestsTimeout = requestsTimeout;
            PerFileSize = perFileSize;
        }
    }
}