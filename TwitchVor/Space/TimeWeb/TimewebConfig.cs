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

        public TimewebConfig(string refreshToken, string? accessToken, DateTimeOffset? accessTokenExpirationDate, bool validateTokenOnStart)
        {
            RefreshToken = refreshToken;
            AccessToken = accessToken;
            AccessTokenExpirationDate = accessTokenExpirationDate;
            ValidateTokenOnStart = validateTokenOnStart;
        }
    }
}