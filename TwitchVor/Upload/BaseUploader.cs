using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Vvideo;

namespace TwitchVor.Upload
{
    abstract class BaseUploader
    {
        protected Guid guid;

        /// <summary>
        /// В байтах
        /// </summary>
        public abstract long SizeLimit { get; }

        public abstract TimeSpan DurationLimit { get; }

        protected BaseUploader(Guid guid)
        {
            this.guid = guid;
        }

        public abstract Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content);
    }
}