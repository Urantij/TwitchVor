using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Configuration
{
    public class ConversionConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string FfmpegPath { get; set; } = Path.Combine("./ffmpeg", "ffmpeg");

        [JsonProperty(Required = Required.Always)]
        public string TargetFormat { get; set; } = "mp4";

        [JsonProperty(Required = Required.Default)]
        public string Arguments { get; set; } = "-movflags isml+frag_keyframe -c copy";
    }
}