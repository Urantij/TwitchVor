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

        public TimewebConfig(string refreshToken)
        {
            RefreshToken = refreshToken;
        }
    }
}