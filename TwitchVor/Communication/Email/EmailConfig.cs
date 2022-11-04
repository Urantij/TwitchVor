using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Communication.Email
{
    public class EmailConfig
    {
#nullable disable
        [JsonProperty(Required = Required.Always)]
        public string Name { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string Email { get; set; }
        [JsonProperty(Required = Required.Always)]
        public string Password { get; set; }
#nullable enable

        [JsonProperty(Required = Required.Default)]
        public bool NotifyOnCriticalError { get; set; } = false;

        [JsonProperty(Required = Required.Default)]
        public bool NotifyOnFinishSuccess { get; set; } = false;
    }
}