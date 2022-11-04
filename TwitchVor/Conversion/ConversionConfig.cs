using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Conversion
{
    public class ConversionConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string FfmpegPath { get; set; } = Path.Combine("./ffmpeg", "ffmpeg");
    }
}